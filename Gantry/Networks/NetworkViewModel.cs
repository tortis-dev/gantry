using Docker.DotNet.Models;
using Serilog;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Gantry.Networks;

class NetworkViewModel : ObservableObject
{
    public NetworkViewModel()
    {
        _ = LoadNetworkList();
    }

    public ObservableCollection<NetworkListItem> Networks { get; } = [];

    async Task LoadNetworkList()
    {
        var client = DockerClientFactory.Create();
        var networkList = await client.Networks.ListNetworksAsync(new NetworksListParameters());

        foreach (var network in networkList)
        {
            Networks.Add(new NetworkListItem
            {
                Id = network.ID,
                Name = network.Name,
                Driver = network.Driver,
                Scope = network.Scope,
                Internal = network.Internal,
                EnableIPv6 = network.EnableIPv6,
                ContainersCount = network.Containers?.Count ?? 0
            });
        }
    }

    public RemoveNetworkCommand RemoveNetworkCommand { get; } = new();
}

class RemoveNetworkCommand : ICommand
{
    public bool CanExecute(object? parameter)
    {
        return parameter is NetworkListItem { ContainersCount: 0 };
    }

    public void Execute(object? parameter)
    {
        if (parameter is not NetworkListItem network) return;
        var id = network.Id;
        _ = RemoveNetwork(id);
    }

    public event EventHandler? CanExecuteChanged;

    async Task RemoveNetwork(string id)
    {
        try
        {
            var client = DockerClientFactory.Create();
            await client.Networks.DeleteNetworkAsync(id);
        }
        catch (Exception e)
        {
            Log.Error(e, "Error removing network.");
        }
    }
}
