using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace PoseBrowser.Core;

internal class DalamudServices
{
    [PluginService] public IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] public IFramework Framework { get; private set; } = null!;
    [PluginService] public ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] public ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] public IClientState ClientState { get; private set; } = null!;
    [PluginService] public IPluginLog Log { get; private set; } = null!;
    [PluginService] public IKeyState KeyState { get; private set; } = null!;



    public DalamudServices(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.Inject(this);
    }
}
