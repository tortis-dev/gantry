using Docker.DotNet.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Gantry.Containers;

class ContainerViewModel : ObservableObject
{
    private ContainerListItem? _selectedContainer;
    private string _logs = string.Empty;
    private CancellationTokenSource? _logStreamCts;

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
            }
        }
    }

    public string Logs
    {
        get => _logs;
        set => SetField(ref _logs, value);
    }

    async Task LoadContainerList()
    {
        using var client = new DockerClientFactory().Create();
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

        if (SelectedContainer == null)
            return;

        _logStreamCts = new CancellationTokenSource();
        var containerId = SelectedContainer.Id;

        try
        {
            using var client = new DockerClientFactory().Create();
            var logParams = new ContainerLogsParameters
            {
                ShowStdout = true,
                ShowStderr = true,
                Follow = true,
                Timestamps = true
            };

            var stream = await client.Containers.GetContainerLogsAsync(containerId, false, logParams, _logStreamCts.Token);
            var buffer = new byte[4096];
            var logBuilder = new StringBuilder();

            while (!_logStreamCts.Token.IsCancellationRequested)
            {
                var result = await stream.ReadOutputAsync(buffer, 0, buffer.Length, _logStreamCts.Token);
                if (result.EOF)
                    break;

                var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
                logBuilder.Append(text);
                Logs = logBuilder.ToString();
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
    }

    public void ClearLogs()
    {
        Logs = string.Empty;
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
            using var client = new DockerClientFactory().Create();
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
            Console.WriteLine(e);
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
            using var client = new DockerClientFactory().Create();
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
            Console.WriteLine(e);
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
            using var client = new DockerClientFactory().Create();
            await client.Containers.RemoveContainerAsync(id, new ContainerRemoveParameters());
            var container = _viewModel.Containers.First(c => c.Id == id);
            _viewModel.Containers.Remove(container);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
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

