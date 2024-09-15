using System;

namespace PoseBrowser.Files;

internal class CMToolPoseFileInfo
{
    public string Name => "CMTool Pose File";
    public string Extension => ".cmp";
}

[Serializable]
internal class CMToolPoseFile
{
    public string? Race { get; set; }
}
