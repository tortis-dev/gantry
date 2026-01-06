using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;
using System.IO;
using System.Linq;

namespace Gantry;

public partial class About : Window
{
    public About()
    {
        InitializeComponent();
    }

    public string Version => $"Version: {(GetType().Assembly.GetName().Version?.ToString(3) ?? "Unknown")}";

    public string License => File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"LICENSE.md"));

    public string DockerContext => $"Docker Context: {new DockerClientFactory().Create().Configuration.EndpointBaseUri.ToString()}";

    public Uri Sbom => new Uri($"file://{Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"sbom.html")}");
}