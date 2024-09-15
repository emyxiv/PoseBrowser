
using System;
using PoseBrowser.Library.Tags;

namespace PoseBrowser.Files;



[Serializable]
internal abstract class JsonDocumentBase : IFileMetadata
{
    public string? Author { get; set; }
    public string? Description { get; set; }
    public string? Version { get; set; }
    public string? Base64Image { get; set; }
    public TagCollection? Tags { get; set; }

    public virtual void GetAutoTags(ref TagCollection tags)
    {
        if(this.Author != null)
        {
            tags.Add(this.Author);
        }
    }
}
public interface IFileMetadata
{
    string? Author { get; }
    string? Description { get; }
    string? Version { get; }
    TagCollection? Tags { get; }

    void GetAutoTags(ref TagCollection tags);
}
