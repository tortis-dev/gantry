using Docker.DotNet;
using System;

namespace Gantry;

public static class DockerClientFactory
{
    static Lazy<DockerClientConfiguration> _config = new Lazy<DockerClientConfiguration>(() => new DockerClientConfiguration());
    static Lazy<IDockerClient> _instance = new Lazy<IDockerClient>(() => _config.Value.CreateClient());

    public static IDockerClient Create() => _instance.Value;
    public static DockerClientConfiguration Configuration => _config.Value;
}