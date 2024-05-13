using System;
using System.Net;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform;
using AvaloniaWebView;
using Material.Styles.Themes;
using Microsoft.Extensions.DependencyInjection;
using YoutubeDownloader.Framework;
using YoutubeDownloader.Services;
using YoutubeDownloader.Utils;
using YoutubeDownloader.Utils.Extensions;
using YoutubeDownloader.ViewModels;
using YoutubeDownloader.ViewModels.Components;
using YoutubeDownloader.ViewModels.Dialogs;
using YoutubeDownloader.Views;

namespace YoutubeDownloader;

public class App : Application, IDisposable
{
    private readonly ServiceProvider _services;
    private readonly SettingsService _settingsService;
    private readonly MainViewModel _mainViewModel;

    private readonly DisposableCollector _eventRoot = new();

    public App()
    {
        var services = new ServiceCollection();

        // Framework
        services.AddSingleton<DialogManager>();
        services.AddSingleton<SnackbarManager>();
        services.AddSingleton<ViewManager>();
        services.AddSingleton<ViewModelManager>();

        // Services
        services.AddSingleton<SettingsService>();
        services.AddSingleton<UpdateService>();

        // View models
        services.AddTransient<MainViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<DownloadViewModel>();
        services.AddTransient<AuthSetupViewModel>();
        services.AddTransient<DownloadMultipleSetupViewModel>();
        services.AddTransient<DownloadSingleSetupViewModel>();
        services.AddTransient<MessageBoxViewModel>();
        services.AddTransient<SettingsViewModel>();

        _services = services.BuildServiceProvider(true);
        _settingsService = _services.GetRequiredService<SettingsService>();
        _mainViewModel = _services.GetRequiredService<ViewModelManager>().CreateMainViewModel();

        // Re-initialize the theme when the user changes it
        _eventRoot.Add(
            _settingsService.WatchProperty(
                o => o.Theme,
                () =>
                {
                    RequestedThemeVariant = _settingsService.Theme switch
                    {
                        ThemeVariant.System => Avalonia.Styling.ThemeVariant.Default,
                        ThemeVariant.Light => Avalonia.Styling.ThemeVariant.Light,
                        ThemeVariant.Dark => Avalonia.Styling.ThemeVariant.Dark,
                        _
                            => throw new InvalidOperationException(
                                $"Unknown theme '{_settingsService.Theme}'."
                            )
                    };

                    InitializeTheme();
                },
                false
            )
        );
    }

    public override void Initialize()
    {
        base.Initialize();

        // Increase maximum concurrent connections
        ServicePointManager.DefaultConnectionLimit = 20;

        AvaloniaXamlLoader.Load(this);
    }

    public override void RegisterServices()
    {
        base.RegisterServices();

        AvaloniaWebViewBuilder.Initialize(config => config.IsInPrivateModeEnabled = true);
    }

    private void InitializeTheme()
    {
        var actualTheme = RequestedThemeVariant?.Key switch
        {
            "Light" => PlatformThemeVariant.Light,
            "Dark" => PlatformThemeVariant.Dark,
            _ => PlatformSettings?.GetColorValues().ThemeVariant
        };

        this.LocateMaterialTheme<MaterialThemeBase>().CurrentTheme = actualTheme switch
        {
            PlatformThemeVariant.Light
                => Theme.Create(Theme.Light, Color.Parse("#343838"), Color.Parse("#F9A825")),
            PlatformThemeVariant.Dark
                => Theme.Create(Theme.Dark, Color.Parse("#E8E8E8"), Color.Parse("#F9A825")),
            _ => throw new InvalidOperationException($"Unknown theme '{actualTheme}'.")
        };

        Resources["SuccessBrush"] = actualTheme switch
        {
            PlatformThemeVariant.Light => new SolidColorBrush(Colors.DarkGreen),
            PlatformThemeVariant.Dark => new SolidColorBrush(Colors.LightGreen),
            _ => throw new InvalidOperationException($"Unknown theme '{actualTheme}'.")
        };

        Resources["CanceledBrush"] = actualTheme switch
        {
            PlatformThemeVariant.Light => new SolidColorBrush(Colors.DarkOrange),
            PlatformThemeVariant.Dark => new SolidColorBrush(Colors.Orange),
            _ => throw new InvalidOperationException($"Unknown theme '{actualTheme}'.")
        };

        Resources["FailedBrush"] = actualTheme switch
        {
            PlatformThemeVariant.Light => new SolidColorBrush(Colors.DarkRed),
            PlatformThemeVariant.Dark => new SolidColorBrush(Colors.OrangeRed),
            _ => throw new InvalidOperationException($"Unknown theme '{actualTheme}'.")
        };
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainView { DataContext = _mainViewModel };

        base.OnFrameworkInitializationCompleted();

        // Set up custom theme colors
        InitializeTheme();

        // Load settings
        _settingsService.Load();
    }

    private void Application_OnActualThemeVariantChanged(object? sender, EventArgs args) =>
        // Re-initialize the theme when the system theme changes
        InitializeTheme();

    public void Dispose()
    {
        _eventRoot.Dispose();
        _services.Dispose();
    }
}
