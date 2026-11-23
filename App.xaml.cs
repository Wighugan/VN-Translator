using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Interop;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Extensions.DependencyInjection;
using VisualNovelDetect.Native;
using VisualNovelDetect.Services.Audio;
using VisualNovelDetect.Services.Cache;
using VisualNovelDetect.Services.Capture;
using VisualNovelDetect.Services.Dictionary;
using VisualNovelDetect.Services.Hotkey;
using VisualNovelDetect.Services.Lookup;
using VisualNovelDetect.Services.Ocr;
using VisualNovelDetect.Services.Settings;
using VisualNovelDetect.Services.Translation;
using VisualNovelDetect.ViewModels;
using VisualNovelDetect.Views;

namespace VisualNovelDetect;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;
    private TaskbarIcon? _trayIcon;
    private PopupWindow? _popupWindow;
    private SettingsWindow? _settingsWindow;
    private IGlobalHookService? _hookService;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 1. Enforce Per-Monitor V2 DPI awareness
        try
        {
            Win32.SetProcessDpiAwarenessContext(Win32.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
        }
        catch { }

        // 2. Global Exception Handling
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            MessageBox.Show($"An unhandled error occurred: {args.ExceptionObject}", "Visual Novel Detect Error", MessageBoxButton.OK, MessageBoxImage.Error);
        };

        // 3. Configure Dependency Injection
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        // 4. Instantiate and configure UI windows
        var popupVm = _serviceProvider.GetRequiredService<PopupViewModel>();
        _popupWindow = new PopupWindow(popupVm);

        // Force HWND creation for hotkey hook registration
        var helper = new WindowInteropHelper(_popupWindow);
        helper.EnsureHandle();

        var settingsVm = _serviceProvider.GetRequiredService<SettingsViewModel>();
        _settingsWindow = new SettingsWindow(settingsVm, _serviceProvider);

        // 5. Setup Lookup Coordinator & Wire Events
        var lookupCoordinator = _serviceProvider.GetRequiredService<ILookupCoordinator>();

        lookupCoordinator.LookupStarted += wordInfo =>
        {
            Dispatcher.Invoke(() => popupVm.PrepareForLookup(wordInfo));
        };

        lookupCoordinator.LookupCompleted += (wordInfo, dict, trans) =>
        {
            Dispatcher.Invoke(() => popupVm.PopulateResults(wordInfo, dict, trans));
        };

        lookupCoordinator.LookupFailed += errorMsg =>
        {
            Dispatcher.Invoke(() => popupVm.ShowError(errorMsg));
        };

        // 6. Setup Global Hotkey and Mouse Hook
        _hookService = _serviceProvider.GetRequiredService<IGlobalHookService>();
        _hookService.Start(helper.Handle);
        _hookService.TriggerActivated += async () =>
        {
            await lookupCoordinator.TriggerLookupAtCursorAsync();
        };

        // 7. Setup System Tray Icon
        SetupSystemTray();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // HTTP Client
        services.AddSingleton<HttpClient>();

        // Core Services
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IScreenCaptureService, ScreenCaptureService>();
        services.AddSingleton<IImagePreprocessor, ImagePreprocessor>();
        services.AddSingleton<WindowsMediaOcrService>();
        services.AddSingleton<TesseractOcrService>();
        services.AddSingleton<IDictionaryService, CambridgeDictionaryService>();
        services.AddSingleton<ITranslationService, GoogleTranslationService>();
        services.AddSingleton<IWordCacheService, SqliteWordCacheService>();
        services.AddSingleton<IAudioPlayerService, AudioPlayerService>();
        services.AddSingleton<IGlobalHookService, GlobalHookService>();
        services.AddSingleton<ILookupCoordinator, LookupCoordinator>();

        // ViewModels
        services.AddSingleton<PopupViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddTransient<RegionSelectorViewModel>();
        services.AddSingleton<TrayViewModel>();
    }

    private void SetupSystemTray()
    {
        var trayVm = _serviceProvider!.GetRequiredService<TrayViewModel>();

        trayVm.RequestOpenSettings += () =>
        {
            Dispatcher.Invoke(() =>
            {
                if (_settingsWindow != null)
                {
                    _settingsWindow.Show();
                    _settingsWindow.Activate();
                }
            });
        };

        trayVm.RequestSelectRegion += () =>
        {
            Dispatcher.Invoke(() =>
            {
                if (_serviceProvider != null)
                {
                    var regionVm = _serviceProvider.GetRequiredService<RegionSelectorViewModel>();
                    var regionWin = new RegionSelectorWindow(regionVm);
                    regionWin.ShowDialog();
                }
            });
        };

        trayVm.RequestExit += () =>
        {
            Dispatcher.Invoke(Shutdown);
        };

        _trayIcon = new TaskbarIcon
        {
            ToolTipText = trayVm.TooltipText,
            ContextMenu = (System.Windows.Controls.ContextMenu)Resources["TrayContextMenu"],
            DataContext = trayVm
        };

        // Generate a crisp, clean app icon dynamically to avoid missing asset dependencies
        _trayIcon.Icon = CreateTrayIcon();
    }

    private static Icon CreateTrayIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Dark background circle with cyan ring
        using var bgBrush = new SolidBrush(Color.FromArgb(15, 23, 42));
        using var ringPen = new Pen(Color.FromArgb(56, 189, 248), 2.5f);
        g.FillEllipse(bgBrush, 2, 2, 28, 28);
        g.DrawEllipse(ringPen, 2, 2, 28, 28);

        // Letter "V"
        using var textBrush = new SolidBrush(Color.FromArgb(248, 250, 252));
        using var font = new Font("Segoe UI", 13, System.Drawing.FontStyle.Bold, GraphicsUnit.Pixel);
        var sf = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        g.DrawString("VN", font, textBrush, new RectangleF(0, 1, 32, 32), sf);

        IntPtr hIcon = bmp.GetHicon();
        return Icon.FromHandle(hIcon);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hookService?.Dispose();
        _trayIcon?.Dispose();
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
