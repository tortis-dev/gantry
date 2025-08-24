using System.Collections.Generic;

namespace Gantry.Images;

class ImageListItem
{
    string _repository = string.Empty;
    private string _tag = string.Empty;

    public ImageListItem(string id, long size, long containersCount)
    {
        Id = id;
        Size = size;
        ContainersCount = containersCount;
    }

    public string Id { get; }
    public string Repository => _repository;
    public string Tag => _tag;
    public long ContainersCount { get; set; }

    public void SetRepositoryAndTag(IList<string> repoTags)
    {
        var parts = repoTags[0].Split(':');
        _repository = parts[0];
        _tag = parts[1];
    }

    public long Size { get; set; }

    public string DisplaySize => FormatSize(Size);

    private string FormatSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;

        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size = size / 1024;
        }

        return $"{size:0.##} {sizes[order]}";
    }
}
