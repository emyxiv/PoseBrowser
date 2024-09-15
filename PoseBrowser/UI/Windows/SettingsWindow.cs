using System.Numerics;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using PoseBrowser.Config;
using PoseBrowser.Input;
using PoseBrowser.IPC;

namespace PoseBrowser.UI.Windows;

internal class SettingsWindow : Window
{
    private readonly ConfigurationService _configurationService;
    private readonly BrioService _brioService;

    // We give this window a constant ID using ###
    // This allows for labels being dynamic, like "{FPS Counter}fps###XYZ counter window",
    // and the window ID will always be "###XYZ counter window" for ImGui
    public SettingsWindow(
        ConfigurationService configurationService,
        BrioService brioService) : base($"{PoseBrowser.Name} Settings###pose_browser_settings_window", ImGuiWindowFlags.NoResize)
    { 
        Namespace = "pose_browser_settings_namespace";
        _configurationService = configurationService;
        _brioService = brioService;

        Size = new Vector2(400, 450);
        
    }
    

    public override void Draw()
    {
       using(ImRaii.PushId("brio_settings"))
       {
           using (var tab = ImRaii.TabBar("###pose_browser_settings_tabs"))
           {
               if (tab.Success)
               {
                   DrawGeneralTab();
                   DrawIPCTab();
                   DrawKeysTab();
               }
           }
       }
    }

    private void DrawGeneralTab()
    {
        using(var tab = ImRaii.TabItem("General"))
        {
            if(tab.Success)
            {
                ImGui.Text("some general stuff");
            }
        }
    }

    private void DrawIPCTab()
    {
        using(var tab = ImRaii.TabItem("IPC"))
        {
            if(tab.Success)
            {
                DrawBrioIPC();
            }
        }
    }

    private void DrawBrioIPC()
    {
        ImGui.Text(_brioService.IsBrioAvailable ? "Brio available" : "No brio available");
        if (!_brioService.IsBrioAvailable) return;
        
        ImGui.Text($"Version {_brioService.ApiVersion()}");
    }
    
    private void DrawKeysTab()
    {
        using(var tab = ImRaii.TabItem("Key Binds"))
        {
            if(!tab.Success)
                return;

            bool enableKeybinds = _configurationService.Configuration.Input.EnableKeybinds;
            if(ImGui.Checkbox("Enable keyboard shortcuts", ref enableKeybinds))
            {
                _configurationService.Configuration.Input.EnableKeybinds = enableKeybinds;
                _configurationService.ApplyChange();
            }

            if(enableKeybinds == false)
            {
                ImGui.BeginDisabled();
            }
            
            if(ImGui.CollapsingHeader("Interface", ImGuiTreeNodeFlags.DefaultOpen))
            {
                DrawKeyBind(KeyBindEvents.Interface_TogglePoseBrowserWindow);
            }

            if(ImGui.CollapsingHeader("Posing", ImGuiTreeNodeFlags.DefaultOpen))
            {
                DrawKeyBind(KeyBindEvents.Posing_PreviewHovering);
            }

            if(enableKeybinds == false)
            {
                ImGui.EndDisabled();
            }

        }
    }
    private void DrawKeyBind(KeyBindEvents evt)
    {
        string evtText = evt.ToString();

        if(KeybindEditor.KeySelector(evtText, evt, _configurationService.Configuration.Input))
        {
            _configurationService.ApplyChange();
        }

    }
    
    
    private void DrawDebugTab()
    {
        using(var tab = ImRaii.TabItem("Debug"))
        {
            if(tab.Success)
            {
                DrawDebugStuff();
            }
        }
    }
    public void DrawDebugStuff()
    {
        string path = @"D:\Games\SquareEnix_ffxiv_things\Poses\2.duo\bff\[Mika] Friendly\[Mika] Friendly - Smirk.pose";

        if (ImGui.Button("Apply Pose"))
        {
            _brioService.ImportPoseTarget(path);
        }

        if (ImGui.Button("Undo"))
        {
            _brioService.UndoTarget();
        }



    }
}
