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

    // Terminal-related fields
    private string _terminalOutput = string.Empty;
    private bool _isTerminalActive = false;
    private CancellationTokenSource? _terminalStreamCts;
    private MultiplexedStream? _terminalStream;
    private string? _detectedShell;
    private static readonly string[] ShellCandidates = new[]
    {
        "/bin/bash",
        "/bin/sh",
        "/bin/ash",
        "/bin/zsh"
    };

    public ContainerViewModel()
    {
        StartContainerCommand = new(this);
        StopContainerCommand = new(this);
        RemoveContainerCommand = new(this);
        ClearLogsCommand = new(this);
        SendTerminalInputCommand = new(this);

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
                _ = StartTerminalSession();
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

    public string TerminalOutput
    {
        get => _terminalOutput;
        set => SetField(ref _terminalOutput, value);
    }

    public bool IsTerminalActive
    {
        get => _isTerminalActive;
        set => SetField(ref _isTerminalActive, value);
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
    public SendTerminalInputCommand SendTerminalInputCommand { get; }

    public void ContainerStateChanged(ContainerListItem container)
    {
        StopContainerCommand.RaiseCanExecuteChanged();
        StartContainerCommand.RaiseCanExecuteChanged();
        RemoveContainerCommand.RaiseCanExecuteChanged();

        // If selected container stopped, deactivate terminal
        if (SelectedContainer?.Id == container.Id && container.State != "running")
        {
            _terminalStreamCts?.Cancel();
            IsTerminalActive = false;
            TerminalOutput += "\n[Container stopped - terminal disconnected]\n";
        }
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

    async Task StartTerminalSession()
    {
        // Cancel any existing terminal session
        _terminalStreamCts?.Cancel();
        _terminalStreamCts?.Dispose();
        _terminalStream?.Dispose();

        TerminalOutput = string.Empty;
        IsTerminalActive = false;

        if (SelectedContainer == null || SelectedContainer.State != "running")
        {
            if (SelectedContainer != null && SelectedContainer.State != "running")
            {
                TerminalOutput = "Terminal is only available for running containers.\n";
            }
            return;
        }

        try
        {
            _terminalStreamCts = new CancellationTokenSource();
            var client = DockerClientFactory.Create();

            // Detect available shell
            _detectedShell = await DetectShellAsync(SelectedContainer.Id, _terminalStreamCts.Token);

            if (_detectedShell == null)
            {
                TerminalOutput = "No shell detected in container. Tried: bash, sh, ash, zsh\n";
                return;
            }

            // Create exec instance
            var execCreateResponse = await client.Exec.ExecCreateContainerAsync(
                SelectedContainer.Id,
                new ContainerExecCreateParameters
                {
                    AttachStdin = true,
                    AttachStdout = true,
                    AttachStderr = true,
                    Tty = true,
                    Cmd = new[] { _detectedShell }
                }
            );

            // Start and attach to exec session
            _terminalStream = await client.Exec.StartAndAttachContainerExecAsync(
                execCreateResponse.ID,
                false,
                _terminalStreamCts.Token
            );

            IsTerminalActive = true;
            TerminalOutput = $"Connected to {_detectedShell}\n";

            // Start output reader task
            _ = ReadTerminalOutputAsync();
        }
        catch (OperationCanceledException)
        {
            // Expected when canceling
        }
        catch (Exception e)
        {
            TerminalOutput = $"Error starting terminal: {e.Message}\n";
            Log.Error(e, "Error starting terminal session");
            IsTerminalActive = false;
        }
    }

    async Task<string?> DetectShellAsync(string containerId, CancellationToken ct)
    {
        var client = DockerClientFactory.Create();

        foreach (var shell in ShellCandidates)
        {
            try
            {
                // Try to create a test exec to see if shell exists
                var testExec = await client.Exec.ExecCreateContainerAsync(
                    containerId,
                    new ContainerExecCreateParameters
                    {
                        AttachStdout = true,
                        Cmd = new[] { "test", "-x", shell }
                    },
                    ct
                );

                // If we got here without exception, shell likely exists
                return shell;
            }
            catch
            {
                // This shell doesn't exist, try next
                continue;
            }
        }

        // Fallback: just try /bin/sh without detection
        return "/bin/sh";
    }

    async Task ReadTerminalOutputAsync()
    {
        if (_terminalStream == null || _terminalStreamCts == null)
            return;

        var buffer = new byte[4096];

        try
        {
            while (!_terminalStreamCts.Token.IsCancellationRequested)
            {
                var result = await _terminalStream.ReadOutputAsync(
                    buffer, 0, buffer.Length, _terminalStreamCts.Token
                );

                if (result.EOF)
                {
                    TerminalOutput += "\n[Terminal session ended - shell exited]\n";
                    IsTerminalActive = false;
                    break;
                }

                var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
                TerminalOutput += text;
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when canceling
        }
        catch (Exception e)
        {
            TerminalOutput += $"\n\nError reading terminal output: {e.Message}\n";
            Log.Error(e, "Error reading terminal output");
            IsTerminalActive = false;
        }
    }

    public async Task SendTerminalInputAsync(string? input)
    {
        if (string.IsNullOrEmpty(input) || _terminalStream == null || !IsTerminalActive)
            return;

        try
        {
            var bytes = Encoding.UTF8.GetBytes(input + "\n");
            await _terminalStream.WriteAsync(bytes, 0, bytes.Length, _terminalStreamCts!.Token);
        }
        catch (Exception e)
        {
            TerminalOutput += $"\nError sending input: {e.Message}\n";
            Log.Error(e, "Error sending terminal input");
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

class SendTerminalInputCommand : ICommand
{
    private readonly ContainerViewModel _viewModel;

    public SendTerminalInputCommand(ContainerViewModel viewModel)
    {
        _viewModel = viewModel;
    }

    public bool CanExecute(object? parameter)
    {
        return _viewModel.IsTerminalActive && !string.IsNullOrWhiteSpace(parameter as string);
    }

    public void Execute(object? parameter)
    {
        if (parameter is string input)
        {
            _ = _viewModel.SendTerminalInputAsync(input);
        }
    }

    public event EventHandler? CanExecuteChanged;

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}

