using System;
using System.Linq;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using PoseBrowser.Config;

namespace PoseBrowser.IPC;

internal class BrioService : IDisposable
{
    public bool IsBrioAvailable { get; private set; } = false;
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly ConfigurationService _configurationService;
    private readonly ITargetManager _targetManager;


    private Brio.API.ApiVersion ApiVersionSubscriber;
    private Brio.API.LoadPoseFromFile LoadPoseFromFileSubscriber;
    private Brio.API.GetPoseAsJson GetPoseAsJsonSubscriber;
    private Brio.API.LoadPoseFromJson LoadPoseFromJsonSubscriber;
    private Brio.API.ResetPose ResetPoseSubscriber;



    public BrioService(IDalamudPluginInterface pluginInterface, ConfigurationService configurationService, ITargetManager targetManager)
    {
        _pluginInterface = pluginInterface;
        _configurationService = configurationService;
        _targetManager = targetManager;


        ApiVersionSubscriber = new global::Brio.API.ApiVersion(pluginInterface);
        GetPoseAsJsonSubscriber = new global::Brio.API.GetPoseAsJson(pluginInterface);
        LoadPoseFromFileSubscriber = new global::Brio.API.LoadPoseFromFile(pluginInterface);
        LoadPoseFromJsonSubscriber = new global::Brio.API.LoadPoseFromJson(pluginInterface);
        ResetPoseSubscriber = new global::Brio.API.ResetPose(pluginInterface);

        RefreshBrioStatus();

        _configurationService.OnConfigurationChanged += RefreshBrioStatus;
    }

    public (int, int) ApiVersion()
    {
        return ApiVersionSubscriber.Invoke();
    }
    public bool ImportPoseTarget(string path)
    {
        var gameObject = GetTargetGameObject();
        if(gameObject == null) return false;

        // save current pose
        var savingJson = GetPoseAsJsonSubscriber?.Invoke(gameObject);
        if(savingJson == null) return false;
        LastPoseSaved = savingJson;

        // apply pose
        return LoadPoseFromFileSubscriber?.Invoke(gameObject, path) ?? false;
    }
    private IGameObject? GetTargetGameObject()
    {
        if (_targetManager.GPoseTarget != null && _targetManager.GPoseTarget.ObjectKind == ObjectKind.Pc) {
            var obj = _targetManager.GPoseTarget;
            PoseBrowser.Log.Debug($"object found: {obj.Name}");
            return obj;

        }
        return null;

    }


    private string? LastPoseSaved = null;
    public bool UndoTarget()
    {
        // verify if there is any pose to restore
        if(LastPoseSaved == null) return false;

        var gameObject = GetTargetGameObject();
        if(gameObject == null) return false;

        if (_configurationService.Configuration.IPC.SaveAndResporePoseInsteadOfReset) {
            return LoadPoseFromJsonSubscriber?.Invoke(gameObject, LastPoseSaved, false) ?? false;
        }
        return ResetPoseSubscriber?.Invoke(gameObject, false) ?? false;

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
