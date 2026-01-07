using Avalonia.Controls;
using Avalonia.Input;

namespace Gantry.Containers;

public partial class ContainerList : UserControl
{
    public ContainerList()
    {
        InitializeComponent();
    }

    void Grid_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
    }

    void TerminalInputBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is ContainerViewModel vm)
        {
            var textBox = sender as TextBox;
            if (textBox != null && !string.IsNullOrWhiteSpace(textBox.Text))
            {
                _ = vm.SendTerminalInputAsync(textBox.Text);
                textBox.Text = string.Empty;  // Clear input after sending
            }
            e.Handled = true;
        }
    }
}