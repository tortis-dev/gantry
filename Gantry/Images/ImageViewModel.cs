using Docker.DotNet.Models;
using Serilog;
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
        RemoveImageCommand = new(this);
        RefreshCommand = new(this);
        _ = LoadImageList();
    }

    public RemoveImageCommand RemoveImageCommand { get; }
    public RefreshCommand RefreshCommand { get; }

    public ObservableCollection<ImageListItem> Images { get; } = [];

    internal async Task LoadImageList()
    {
        var client = DockerClientFactory.Create();
        var imageList = await client.Images.ListImagesAsync(new ImagesListParameters());
        foreach (var image in imageList)
        {
            var item = new ImageListItem(
                id: image.ID,
                size: image.Size,
                containersCount: image.Containers);
            if (image.RepoTags is not null && image.RepoTags.Any())
                item.SetRepositoryAndTag(image.RepoTags);
            else
                item.SetRepositoryAndTag(["<none>:<none>"]);
            Images.Add(item);
        }
    }
}

class RemoveImageCommand : ICommand
{
    readonly ImageViewModel _viewModel;

    public RemoveImageCommand(ImageViewModel viewModel)
    {
        _viewModel = viewModel;
    }

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
            var client = DockerClientFactory.Create();
            await client.Images.DeleteImageAsync(id, new ImageDeleteParameters());
            var image = _viewModel.Images.First(i => i.Id == id);
            _viewModel.Images.Remove(image);
        }
        catch (Exception e)
        {
            Log.Error(e, "Error removing image.");
        }
    }
}

class RefreshCommand : ICommand
{
    readonly ImageViewModel _viewModel;

    public RefreshCommand(ImageViewModel viewModel)
    {
        _viewModel = viewModel;
    }

    public bool CanExecute(object? parameter)
    {
        return true;
    }

    public void Execute(object? parameter)
    {
        _viewModel.Images.Clear();
        _ = _viewModel.LoadImageList();
    }

    public event EventHandler? CanExecuteChanged;
}
