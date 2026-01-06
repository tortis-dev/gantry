using Avalonia.Controls;
using System;
using System.IO;

namespace Gantry;

public partial class About : Window
{
    public About()
    {
        InitializeComponent();
    }

    public string Version => $"Version: {(GetType().Assembly.GetName().Version?.ToString(3) ?? "Unknown")}";

    public string License => File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"LICENSE.md"));

    public string DockerContext => $"Docker Context: {DockerClientFactory.Configuration.EndpointBaseUri.ToString()}";

    public Uri Sbom => new Uri($"file://{Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"sbom.html")}");
}