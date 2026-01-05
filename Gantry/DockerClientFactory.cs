using Docker.DotNet;
using System;

namespace Gantry;

public class DockerClientFactory
{
    static Lazy<IDockerClient> _instance = new Lazy<IDockerClient>(() => new DockerClientConfiguration().CreateClient());

    public IDockerClient Create()
    {
        return _instance.Value;
    }
}