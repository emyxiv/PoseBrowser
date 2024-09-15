using System;
using System.Linq;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using PoseBrowser.Config;

namespace PoseBrowser.IPC;

internal class BrioService : IDisposable
{
    public bool IsBrioAvailable { get; private set; } = false;
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly ConfigurationService _configurationService;
    
    public const string ApiVersionIpcName = "Brio.ApiVersion";
    private readonly ICallGateSubscriber<(int, int)>? ApiVersionIpc;
    
    public const string ImportPoseTargetIpcName = "Brio.ImportPoseSelected";
    private readonly ICallGateSubscriber<string, bool>? ImportPoseTargetIpc;

    public const string UndoTargetIpcName = "Brio.UndoSelected";
    private readonly ICallGateSubscriber<bool>? UndoTargetIpc;
    
    


    public BrioService(IDalamudPluginInterface pluginInterface, ConfigurationService configurationService)
    {
        _pluginInterface = pluginInterface;
        _configurationService = configurationService;
        
        ApiVersionIpc = pluginInterface.GetIpcSubscriber<(int,int)>(ApiVersionIpcName);
        ImportPoseTargetIpc = pluginInterface.GetIpcSubscriber<string, bool>(ImportPoseTargetIpcName);
        UndoTargetIpc = pluginInterface.GetIpcSubscriber<bool>(UndoTargetIpcName);
        RefreshBrioStatus();
        
        _configurationService.OnConfigurationChanged += RefreshBrioStatus;
    }

    public (int, int) ApiVersion()
    {
        return ApiVersionIpc?.InvokeFunc() ?? default;
    }
    public bool ImportPoseTarget(string path)
    {
        return ImportPoseTargetIpc?.InvokeFunc(path) ?? false;
    }
    public bool UndoTarget()
    {
        return UndoTargetIpc?.InvokeFunc() ?? false;
    }
    
    
    public void RefreshBrioStatus()
    {
        if(_configurationService.Configuration.IPC.AllowBrioIntegration)
        {
            IsBrioAvailable = ConnectToBrio();
        }
        else
        {
            IsBrioAvailable = false;
        }
    }

    private bool ConnectToBrio()
    {
        try
        {
            bool brioInstalled = _pluginInterface.InstalledPlugins.Any(x => x.Name == "Brio" && x.IsLoaded);

            if(!brioInstalled)
            {
                PoseBrowser.Log.Debug("Brio not present");
                return false;
            }

            PoseBrowser.Log.Debug("Brio integration initialized");

            return true;
        }
        catch(Exception ex)
        {
            PoseBrowser.Log.Debug(ex, "Brio initialize error");
            return false;
        }
    }
    public void Dispose()
    {
        _configurationService.OnConfigurationChanged -= RefreshBrioStatus;
    }


}
