using System.Collections.Generic;

namespace Gantry.Networks;

class NetworkListItem
{
    public string Id { get; set; } = string.Empty;
    public string ShortId => Id.Length > 12 ? Id.Substring(0, 12) : Id;
    public string Name { get; set; } = string.Empty;
    public string Driver { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public bool Internal { get; set; }
    public bool EnableIPv6 { get; set; }
    public Dictionary<string, string> Labels { get; set; } = new();
    public Dictionary<string, object> Options { get; set; } = new();
    public int ContainersCount { get; set; }

    public string DisplayStatus => Internal ? "Internal" : "External";
}
