using Docker.DotNet;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Gantry;

public class MainWindowViewModel
{
    public IDockerClient DockerClient { get; } = new DockerClientFactory().Create();
}
