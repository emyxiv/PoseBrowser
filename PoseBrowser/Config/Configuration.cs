using Dalamud.Configuration;
using Dalamud.Plugin;
using System;

namespace PoseBrowser.Config;

internal class Configuration : IPluginConfiguration
{
    public const int CurrentVersion = 0;

    public int Version { get; set; } = CurrentVersion;

    
    // Appearance
    public AppearanceConfiguration Appearance { get; set; } = new AppearanceConfiguration();

    // Filesystem
    public FilesystemConfiguration Filesystem { get; set; } = new FilesystemConfiguration();
    
    // IPC
    public IPCConfiguration IPC { get; set; } = new IPCConfiguration();

    // Input
    public InputConfiguration Input { get; set; } = new InputConfiguration();

    
    // Developer
    public bool ForceDebug { get; set; } = false;
}
