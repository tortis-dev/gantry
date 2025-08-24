using Docker.DotNet;

namespace Gantry;

public class DockerClientFactory
{
    public IDockerClient Create()
    {
        return new DockerClientConfiguration().CreateClient();
    }
}