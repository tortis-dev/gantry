using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Gantry;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    void Clear()
    {
        ImageListView.IsVisible = false;
        ContainerListView.IsVisible = false;
        VolumeListView.IsVisible = false;
        NetworkListView.IsVisible = false;
    }

    private void Containers_OnClick(object? sender, RoutedEventArgs e)
    {
        Clear();
        ContainerListView.IsVisible = true;
    }

    private void Images_OnClick(object? sender, RoutedEventArgs e)
    {
        Clear();
        ImageListView.IsVisible = true;
    }

    private void Volumes_OnClick(object? sender, RoutedEventArgs e)
    {
        Clear();
        VolumeListView.IsVisible = true;
    }

    private void Networks_OnClick(object? sender, RoutedEventArgs e)
    {
        Clear();
        NetworkListView.IsVisible = true;
    }
}