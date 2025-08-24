using Docker.DotNet.Models;
using System.Collections.Generic;
using System.Linq;

namespace Gantry.Containers;

static class PortExtensions
{
    public static string ToDisplay(this IList<Port> ports)
    {
        return string.Join(",",
            ports.Where(p => p.PublicPort > 0)
                 .Select(p => $"{p.PublicPort.ToString()}:{p.PrivatePort.ToString()}").Distinct());
    }
}