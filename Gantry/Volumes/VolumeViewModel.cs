using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Gantry.Volumes;

class VolumeViewModel : ObservableObject
{
    public VolumeViewModel()
    {
        _ = LoadVolumeList();
    }

    public ObservableCollection<VolumeListItem> Volumes { get; } = [];

    async Task LoadVolumeList()
    {
        using var client = new DockerClientFactory().Create();
        var volumeList = await client.Volumes.ListAsync();

        foreach (var volume in volumeList.Volumes)
        {
            Volumes.Add(new VolumeListItem
            {
                Name = volume.Name,
                Driver = volume.Driver,
                Mountpoint = volume.Mountpoint,
                Scope = volume.Scope,
                CreatedAt = volume.CreatedAt,
                IsInUse = volume.UsageData?.RefCount > 0
            });
        }
    }

    public RemoveVolumeCommand RemoveVolumeCommand { get; } = new();
}

class RemoveVolumeCommand : ICommand
{
    public bool CanExecute(object? parameter)
    {
        return parameter is VolumeListItem { IsInUse: false };
    }

    public void Execute(object? parameter)
    {
        if (parameter is not VolumeListItem volume) return;
        var name = volume.Name;
        _ = RemoveVolume(name);
    }

    public event EventHandler? CanExecuteChanged;

    async Task RemoveVolume(string name)
    {
        try
        {
            using var client = new DockerClientFactory().Create();
            await client.Volumes.RemoveAsync(name);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
}
