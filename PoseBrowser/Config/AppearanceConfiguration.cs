using System.Numerics;

namespace PoseBrowser.Config;

internal class AppearanceConfiguration
{
    public bool BrowserEnableImages { get; set; } = true;
    
    public Vector2 ButtonSize { get; set; } =  new(20, 20);
    public Vector2 BrowserThumbSize { get; set; } =  new(200, 200);
    
    
    
}
