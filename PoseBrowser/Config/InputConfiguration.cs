using PoseBrowser.Input;
using Dalamud.Game.ClientState.Keys;
using System.Collections.Generic;

namespace PoseBrowser.Config;

internal class InputConfiguration
{
    public Dictionary<KeyBindEvents, KeyBind> Bindings { get; set; } = new()
    {
        // Default bindings
        { KeyBindEvents.Interface_TogglePoseBrowserWindow, new(VirtualKey.P, true) },
        { KeyBindEvents.Posing_PreviewHovering, new(VirtualKey.P) },
        
    };
    
    public bool EnableKeybinds { get; set; } = true;
}
