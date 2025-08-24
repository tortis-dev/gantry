using System.Collections.Generic;

namespace Gantry.Volumes;

public class VolumeListItem
{
    public string Name { get; set; } = string.Empty;
    public string Driver { get; set; } = string.Empty;
    public string Mountpoint { get; set; } = string.Empty;
    public Dictionary<string, string> Labels { get; set; } = new();
    public string Scope { get; set; } = string.Empty;
    public Dictionary<string, string> Options { get; set; } = new();
    public string CreatedAt { get; set; } = string.Empty;
    public bool IsInUse { get; set; }

    public string DisplaySize => "--";
    public string Status => IsInUse ? "In use" : "Available";
}
