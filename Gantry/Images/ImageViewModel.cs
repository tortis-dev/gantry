using Docker.DotNet.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Gantry.Images;

class ImageViewModel : ObservableObject
{
    public ImageViewModel()
    {
        _ = LoadImageList();
    }

    public RemoveImageCommand RemoveImageCommand { get; } = new();

    public ObservableCollection<ImageListItem> Images { get; } = [];

    async Task LoadImageList()
    {
        var client = new DockerClientFactory().Create();
        var imageList = await client.Images.ListImagesAsync(new ImagesListParameters());
        var containerList = await client.Containers.ListContainersAsync(new ContainersListParameters {All = true});
        foreach (var image in imageList)
        {
            var item = new ImageListItem(
                id: image.ID,
                size: image.Size,
                containersCount: containerList.Where(c => c.ImageID == image.ID).LongCount());
            item.SetRepositoryAndTag(image.RepoTags);
            Images.Add(item);
        }
    }
}

class RemoveImageCommand : ICommand
{
    public bool CanExecute(object? parameter)
    {
        return parameter is ImageListItem { ContainersCount: 0 };
    }

    public void Execute(object? parameter)
    {
        if (parameter is not ImageListItem image) return;
        var id = image.Id;
        _ = RemoveImage(id);
    }

    public event EventHandler? CanExecuteChanged;

    async Task RemoveImage(string id)
    {
        try
        {
            var client = new DockerClientFactory().Create();
            await client.Images.DeleteImageAsync(id, new ImageDeleteParameters());
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
}

