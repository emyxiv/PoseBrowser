using System;
using System.Diagnostics;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.DependencyInjection;
using PoseBrowser.Config;
using PoseBrowser.Core;
using PoseBrowser.Input;
using PoseBrowser.IPC;
using PoseBrowser.UI;
using PoseBrowser.UI.Windows;

namespace PoseBrowser;

public sealed class PoseBrowser : IDalamudPlugin
{
    public const string Name = "PoseBrowser";

    private static ServiceProvider? _services = null;

    public static IPluginLog Log { get; private set; } = null!;
    public static IFramework Framework { get; private set; } = null!;
    
    public PoseBrowser(IDalamudPluginInterface pluginInterface)
    {
        // Setup dalamud services
        var dalamudServices = new DalamudServices(pluginInterface);
        Log = dalamudServices.Log;
        Framework = dalamudServices.Framework;
        
        dalamudServices.Framework.RunOnTick(() =>
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            
            Log.Info($"Starting {Name}...");

            try
            {
                // Setup plugin services
                var serviceCollection = SetupServices(dalamudServices);

                _services = serviceCollection.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });

                // Initialize the singletons
                foreach(var service in serviceCollection)
                {
                    if(service.Lifetime == ServiceLifetime.Singleton)
                    {
                        Log.Debug($"Initializing {service.ServiceType}...");
                        _services.GetRequiredService(service.ServiceType);
                    }
                }
                
                Log.Info($"Started {Name} in {stopwatch.ElapsedMilliseconds}ms");
            }
            catch(Exception e)
            {
                Log.Error(e, $"Failed to start {Name} in {stopwatch.ElapsedMilliseconds}ms");
                _services?.Dispose();
                throw;
            }
        }, delayTicks: 2); // TODO: Why do we need to wait several frames for some users?
    }
    private static ServiceCollection SetupServices(DalamudServices dalamudServices)
    {
        ServiceCollection serviceCollection = new();

        // Dalamud
        serviceCollection.AddSingleton(dalamudServices.PluginInterface);
        serviceCollection.AddSingleton(dalamudServices.Framework);
        serviceCollection.AddSingleton(dalamudServices.CommandManager);
        serviceCollection.AddSingleton(dalamudServices.TextureProvider);
        serviceCollection.AddSingleton(dalamudServices.ClientState);
        serviceCollection.AddSingleton(dalamudServices.Log);
        serviceCollection.AddSingleton(dalamudServices.KeyState);

        // Core / Misc
        serviceCollection.AddSingleton<ConfigurationService>();
        serviceCollection.AddSingleton<InputService>();

        // IPC
        serviceCollection.AddSingleton<BrioService>();
        
        // UI
        serviceCollection.AddSingleton<UIManager>();
        serviceCollection.AddSingleton<MainWindow>();
        serviceCollection.AddSingleton<SettingsWindow>();

        return serviceCollection;
    }

    public void Dispose()
    {
        _services?.Dispose();
    }
}
