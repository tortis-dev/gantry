using Docker.DotNet.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Gantry.Containers;

class ContainerViewModel : ObservableObject
{
    public ContainerViewModel()
    {
        StartContainerCommand = new(this);
        StopContainerCommand = new(this);
        RemoveContainerCommand = new(this);

        _ = LoadContainerList();
    }
    public ObservableCollection<ContainerListItem> Containers { get; } = [];

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

    public void ContainerStateChanged(ContainerListItem container)
    {
        StopContainerCommand.RaiseCanExecuteChanged();
        StartContainerCommand.RaiseCanExecuteChanged();
        RemoveContainerCommand.RaiseCanExecuteChanged();
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

