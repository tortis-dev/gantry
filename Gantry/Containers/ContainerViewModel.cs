using Docker.DotNet;
using Docker.DotNet.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Gantry.Containers;

class ContainerViewModel : ObservableObject
{
    private const int MaxLogLines = 10000;
    private ContainerListItem? _selectedContainer;
    private string _logs = string.Empty;
    private string _inspectJson = string.Empty;
    private CancellationTokenSource? _logStreamCts;
    private List<string> _logLines = [];

    public ContainerViewModel()
    {
        StartContainerCommand = new(this);
        StopContainerCommand = new(this);
        RemoveContainerCommand = new(this);
        ClearLogsCommand = new(this);

        _ = LoadContainerList();
    }
    public ObservableCollection<ContainerListItem> Containers { get; } = [];

    public ContainerListItem? SelectedContainer
    {
        get => _selectedContainer;
        set
        {
            if (SetField(ref _selectedContainer, value))
            {
                _ = StartLogStream();
                _ = LoadInspectData();
            }
        }
    }

    public string Logs
    {
        get => _logs;
        set => SetField(ref _logs, value);
    }

    public string InspectJson
    {
        get => _inspectJson;
        set => SetField(ref _inspectJson, value);
    }

    async Task LoadContainerList()
    {
        var client = DockerClientFactory.Create();
        IList<ContainerListResponse> containerList = await client.Containers.ListContainersAsync(new ContainersListParameters {All = true} );
        foreach (var response in containerList)
        {
            Containers.Add(new ContainerListItem(
                id: response.ID,
                image: response.Image,
                command: response.Command,
                state: response.State,
                status: response.Status,
                ports: response.Ports.ToDisplay(),
                name: response.Names[0].Substring(1),
                size: response.SizeRw.ToString(),
                labels: response.Labels.Any()
                    ? string.Join(",", response.Labels.Select(l => $"{l.Key}={l.Value}"))
                    : string.Empty));
        }
    }

    public StopContainerCommand StopContainerCommand { get; }
    public StartContainerCommand StartContainerCommand { get; }
    public RemoveContainerCommand RemoveContainerCommand { get; }
    public ClearLogsCommand ClearLogsCommand { get; }

    public void ContainerStateChanged(ContainerListItem container)
    {
        StopContainerCommand.RaiseCanExecuteChanged();
        StartContainerCommand.RaiseCanExecuteChanged();
        RemoveContainerCommand.RaiseCanExecuteChanged();
    }

    async Task StartLogStream()
    {
        // Cancel any existing stream
        _logStreamCts?.Cancel();
        _logStreamCts?.Dispose();

        Logs = string.Empty;
        _logLines.Clear();

        if (SelectedContainer == null)
            return;

        _logStreamCts = new CancellationTokenSource();
        var containerId = SelectedContainer.Id;

        var client = DockerClientFactory.Create();
        var logParams = new ContainerLogsParameters
        {
            ShowStdout = true, ShowStderr = true, Follow = true, Timestamps = true, Tail = "10000"
        };

        while (!_logStreamCts.Token.IsCancellationRequested)
        {
            MultiplexedStream? stream = null;

            try
            {
                await Task.Delay(100, _logStreamCts.Token);

                stream =
                    await client.Containers.GetContainerLogsAsync(containerId, false, logParams, _logStreamCts.Token);

                if (stream is null)
                    continue;

                var buffer = new byte[4096];
                var partialLine = new StringBuilder();

                while (!_logStreamCts.Token.IsCancellationRequested)
                {
                    var result = await stream.ReadOutputAsync(buffer, 0, buffer.Length, _logStreamCts.Token);
                    if (result.EOF)
                    {
                        logParams.Since = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
                        break;
                    }

                    var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    partialLine.Append(text);

                    // Process complete lines
                    var currentText = partialLine.ToString();
                    var lines = currentText.Split('\n');

                    // Last element might be incomplete, keep it in partialLine
                    for (int i = 0; i < lines.Length - 1; i++)
                    {
                        _logLines.Add(lines[i]);
                    }

                    // Keep the last incomplete line in the buffer
                    partialLine.Clear();
                    partialLine.Append(lines[^1]);

                    // Trim to last MaxLogLines
                    if (_logLines.Count > MaxLogLines)
                    {
                        _logLines.RemoveRange(0, _logLines.Count - MaxLogLines);
                    }

                    // Update the display
                    Logs = string.Join('\n', _logLines);
                    if (partialLine.Length > 0)
                    {
                        Logs += partialLine.ToString();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when canceling the stream
            }
            catch (Exception e)
            {
                Logs += $"\n\nError streaming logs: {e.Message}";
            }
            finally
            {
                stream?.Dispose();
                stream = null;
            }
        }
    }

    public void ClearLogs()
    {
        _logLines.Clear();
        Logs = string.Empty;
    }

    async Task LoadInspectData()
    {
        InspectJson = string.Empty;

        if (SelectedContainer == null)
            return;

        try
        {
            var client = DockerClientFactory.Create();
            var response = await client.Containers.InspectContainerAsync(SelectedContainer.Id);

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            InspectJson = JsonSerializer.Serialize(response, options);
        }
        catch (Exception e)
        {
            InspectJson = $"Error loading inspect data: {e.Message}";
            Log.Error(e, "Error loading inspect data");
        }
    }
}

class StopContainerCommand : ICommand
{
    private ContainerViewModel _viewModel;

    public StopContainerCommand(ContainerViewModel viewModel)
    {
        _viewModel = viewModel;
    }

    public bool CanExecute(object? parameter)
    {
        return parameter is ContainerListItem { State: "running" };
    }

    public void Execute(object? parameter)
    {
        if (parameter is not ContainerListItem container) return;
        var id = container.Id;
        _ = StopContainer(id);
    }

    public event EventHandler? CanExecuteChanged;

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    async Task StopContainer(string id)
    {
        try
        {
            var client = DockerClientFactory.Create();
            var result = await client.Containers.StopContainerAsync(id, new ContainerStopParameters());
            if (result)
            {
                var current = (await client.Containers.ListContainersAsync(new ContainersListParameters{All = true}))
                    .First(c => c.ID == id);
                var model = _viewModel.Containers.First(c => c.Id == id);
                model.State = current.State;
                model.Status = current.Status;
                model.Ports = current.Ports.ToDisplay();

                _viewModel.ContainerStateChanged(model);
            }
        }
        catch (Exception e)
        {
            Log.Error(e, "Error stopping container");
        }
    }
}

class StartContainerCommand : ICommand
{
    ContainerViewModel _viewModel;

    public StartContainerCommand(ContainerViewModel viewModel)
    {
        _viewModel = viewModel;
    }

    public bool CanExecute(object? parameter)
    {
        return parameter is ContainerListItem { State: "exited" };
    }

    public void Execute(object? parameter)
    {
        if (parameter is not ContainerListItem container) return;
        var id = container.Id;
        _ = StartContainer(id);
    }

    public event EventHandler? CanExecuteChanged;

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    async Task StartContainer(string id)
    {
        try
        {
            var client = DockerClientFactory.Create();
            var result = await client.Containers.StartContainerAsync(id, new ContainerStartParameters());
            if (result)
            {
                var current = (await client.Containers.ListContainersAsync(new ContainersListParameters()))
                    .First(c => c.ID == id);
                var model = _viewModel.Containers.First(c => c.Id == id);
                model.State = current.State;
                model.Status = current.Status;
                model.Ports = current.Ports.ToDisplay();

                _viewModel.ContainerStateChanged(model);
            }
        }
        catch (Exception e)
        {
            Log.Error(e, "Error starting container");
        }
    }
}

class RemoveContainerCommand : ICommand
{
    private ContainerViewModel _viewModel;

    public RemoveContainerCommand(ContainerViewModel viewModel)
    {
        _viewModel = viewModel;
    }

    public bool CanExecute(object? parameter)
    {
        return parameter is ContainerListItem { State: "exited" };
    }

    public void Execute(object? parameter)
    {
        if (parameter is not ContainerListItem container) return;
        var id = container.Id;
        _ = RemoveContainer(id);
    }

    public event EventHandler? CanExecuteChanged;

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    async Task RemoveContainer(string id)
    {
        try
        {
            var client = DockerClientFactory.Create();
            await client.Containers.RemoveContainerAsync(id, new ContainerRemoveParameters());
            var container = _viewModel.Containers.First(c => c.Id == id);
            _viewModel.Containers.Remove(container);
        }
        catch (Exception e)
        {
            Log.Error(e, "Error removing container");
        }
    }
}

class ClearLogsCommand : ICommand
{
    private ContainerViewModel _viewModel;

    public ClearLogsCommand(ContainerViewModel viewModel)
    {
        _viewModel = viewModel;
    }

    public bool CanExecute(object? parameter)
    {
        return true;
    }

    public void Execute(object? parameter)
    {
        _viewModel.ClearLogs();
    }

    public event EventHandler? CanExecuteChanged;
}

