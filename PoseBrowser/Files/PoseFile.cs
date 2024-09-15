using System;

namespace PoseBrowser.Files;

internal class PoseFileInfo
{
    public string Name => "Pose File";
    public string Extension => ".pose";
}


[Serializable]
internal class PoseFile : JsonDocumentBase
{
    
}

