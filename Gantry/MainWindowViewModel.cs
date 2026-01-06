using Docker.DotNet;

namespace Gantry;

public class MainWindowViewModel
{
    public IDockerClient DockerClient { get; } = new DockerClientFactory().Create();
}
