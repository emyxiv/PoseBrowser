
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ImGuiNET;
using System;
using System.Collections.Generic;
using PoseBrowser.Config;
using PoseBrowser.IPC;
using PoseBrowser.UI.Windows;

namespace PoseBrowser.UI;

internal class UIManager : IDisposable
{
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly ConfigurationService _configurationService;

    private readonly MainWindow _mainWindow;
    private readonly SettingsWindow _settingsWindow;


    private readonly ITextureProvider _textureProvider;
    private readonly IFramework _framework;
    private readonly BrioService _brioService;
    private readonly WindowSystem _windowSystem;

    public readonly FileDialogManager FileDialogManager = new()
    {
        AddedWindowFlags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoDocking
    };

    private readonly List<Window> _hiddenWindows = [];


    public static UIManager Instance { get; private set; } = null!;


    public UIManager
        (
            IDalamudPluginInterface pluginInterface,
            ConfigurationService configurationService,
            IFramework framework,
            ITextureProvider textureProvider,
            MainWindow mainWindow,
            SettingsWindow settingsWindow,
            BrioService brioService
        )
    {
        Instance = this;

        _pluginInterface = pluginInterface;
        _configurationService = configurationService;
        _textureProvider = textureProvider;

        _mainWindow = mainWindow;
        _settingsWindow = settingsWindow;

        _framework = framework;
        _brioService = brioService;

        _windowSystem = new(PoseBrowser.Name);
        _windowSystem.AddWindow(_mainWindow);
        _windowSystem.AddWindow(_settingsWindow);

        _configurationService.OnConfigurationChanged += ApplySettings;

        _pluginInterface.UiBuilder.Draw += DrawUI;
        _pluginInterface.UiBuilder.OpenConfigUi += ShowSettingsWindow;
        _pluginInterface.UiBuilder.OpenMainUi += ShowMainWindow;
        _pluginInterface.ActivePluginsChanged += ActivePluginsChanged;

        ApplySettings();
    }



    public void ShowSettingsWindow()
    {
        _settingsWindow.IsOpen = true;
    }

    public void ShowMainWindow()
    {
        _mainWindow.IsOpen = true;
    }

    private void ActivePluginsChanged(PluginListInvalidationKind kind, bool affectedThisPlugin)
    {
        foreach(var plugin in _pluginInterface.InstalledPlugins)
        {
            PoseBrowser.Log.Debug($"InstalledPlugins: {plugin}");
        }
        
    }
    

    public void ToggleMainWindow() => _mainWindow.IsOpen = !_mainWindow.IsOpen;
    public void ToggleSettingsWindow() => _settingsWindow.IsOpen = !_settingsWindow.IsOpen;




    private void ApplySettings()
    {

    }

    private void DrawUI()
    {
        try
        {
            _windowSystem.Draw();
            FileDialogManager.Draw();
        }
        finally
        {
        }
    }

    public void TemporarilyHideAllOpenWindows()
    {
        foreach(var window in _windowSystem.Windows)
        {
            if(window.IsOpen == true)
            {
                _hiddenWindows.Add(window);
                window.IsOpen = false;
            }
        }
    }

    public void ReopenAllTemporarilyHiddenWindows()
    {
        foreach (var window in _hiddenWindows)
        {
            window.IsOpen = true;
        }
        _hiddenWindows.Clear();
    }

    public void Dispose()
    {
        _configurationService.OnConfigurationChanged -= ApplySettings;
        _pluginInterface.UiBuilder.Draw -= _windowSystem.Draw;
        _pluginInterface.UiBuilder.OpenConfigUi -= ShowSettingsWindow;
        _pluginInterface.UiBuilder.OpenMainUi -= ShowMainWindow;

        _mainWindow.Dispose();

        _windowSystem.RemoveAllWindows();

        Instance = null!;
    }

    public IDalamudTextureWrap LoadImage(byte[] data)
    {
        var imgTask = _textureProvider.CreateFromImageAsync(data);
        imgTask.Wait(); // TODO: Don't block
        var img = imgTask.Result;
        return img;
    }
}
