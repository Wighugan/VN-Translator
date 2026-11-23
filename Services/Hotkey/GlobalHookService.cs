using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using VisualNovelDetect.Native;
using VisualNovelDetect.Services.Settings;

namespace VisualNovelDetect.Services.Hotkey;

public class GlobalHookService : IGlobalHookService
{
    private const int HotkeyId = 9001;

    private readonly ISettingsService _settingsService;
    private IntPtr _windowHandle = IntPtr.Zero;
    private HwndSource? _hwndSource;
    private IntPtr _mouseHookId = IntPtr.Zero;
    private Win32.HookProc? _mouseHookDelegate;
    private bool _isHookActive;

    public event Action? TriggerActivated;

    public GlobalHookService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        _settingsService.SettingsChanged += OnSettingsChanged;
    }

    public void Start(IntPtr windowHandle)
    {
        _windowHandle = windowHandle;
        _hwndSource = HwndSource.FromHwnd(_windowHandle);
        _hwndSource?.AddHook(HwndHook);

        RegisterConfiguredHotkey();
        UpdateMouseHook();
    }

    public void Stop()
    {
        UnregisterHotkey();
        RemoveMouseHook();

        if (_hwndSource != null)
        {
            _hwndSource.RemoveHook(HwndHook);
            _hwndSource = null;
        }
    }

    public void UpdateHotkey()
    {
        RegisterConfiguredHotkey();
        UpdateMouseHook();
    }

    private void OnSettingsChanged(Models.AppSettings settings)
    {
        UpdateHotkey();
    }

    private void RegisterConfiguredHotkey()
    {
        if (_windowHandle == IntPtr.Zero) return;

        UnregisterHotkey();

        var settings = _settingsService.CurrentSettings;
        if (!settings.IsLookupEnabled) return;

        uint modifiers = Win32.MOD_NOREPEAT;
        if (settings.HotkeyCtrl) modifiers |= Win32.MOD_CONTROL;
        if (settings.HotkeyShift) modifiers |= Win32.MOD_SHIFT;
        if (settings.HotkeyAlt) modifiers |= Win32.MOD_ALT;

        uint vk = KeyStringToVk(settings.HotkeyKey);
        if (vk > 0)
        {
            Win32.RegisterHotKey(_windowHandle, HotkeyId, modifiers, vk);
        }
    }

    private void UnregisterHotkey()
    {
        if (_windowHandle != IntPtr.Zero)
        {
            Win32.UnregisterHotKey(_windowHandle, HotkeyId);
        }
    }

    private void UpdateMouseHook()
    {
        var settings = _settingsService.CurrentSettings;
        if (settings.IsLookupEnabled && settings.EnableMiddleClickTrigger)
        {
            InstallMouseHook();
        }
        else
        {
            RemoveMouseHook();
        }
    }

    private void InstallMouseHook()
    {
        if (_isHookActive) return;

        _mouseHookDelegate = MouseHookCallback;
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule;

        IntPtr modHandle = Win32.GetModuleHandle(curModule?.ModuleName);
        _mouseHookId = Win32.SetWindowsHookEx(Win32.WH_MOUSE_LL, _mouseHookDelegate, modHandle, 0);
        _isHookActive = _mouseHookId != IntPtr.Zero;
    }

    private void RemoveMouseHook()
    {
        if (_isHookActive && _mouseHookId != IntPtr.Zero)
        {
            Win32.UnhookWindowsHookEx(_mouseHookId);
            _mouseHookId = IntPtr.Zero;
            _mouseHookDelegate = null;
            _isHookActive = false;
        }
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == (IntPtr)Win32.WM_MBUTTONUP)
        {
            var settings = _settingsService.CurrentSettings;
            if (settings.IsLookupEnabled && settings.EnableMiddleClickTrigger)
            {
                TriggerActivated?.Invoke();
            }
        }

        return Win32.CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Win32.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            TriggerActivated?.Invoke();
            handled = true;
        }

        return IntPtr.Zero;
    }

    public static uint KeyStringToVk(string keyName)
    {
        if (string.IsNullOrWhiteSpace(keyName))
            return (uint)KeyInterop.VirtualKeyFromKey(Key.D);

        keyName = keyName.Trim();

        // Check common symbol aliases
        Key mappedKey = keyName switch
        {
            "0" => Key.D0,
            "1" => Key.D1,
            "2" => Key.D2,
            "3" => Key.D3,
            "4" => Key.D4,
            "5" => Key.D5,
            "6" => Key.D6,
            "7" => Key.D7,
            "8" => Key.D8,
            "9" => Key.D9,
            "`" => Key.OemTilde,
            "-" => Key.OemMinus,
            "+" => Key.OemPlus,
            "[" => Key.OemOpenBrackets,
            "]" => Key.OemCloseBrackets,
            "'" => Key.OemQuotes,
            "/" => Key.OemQuestion,
            "." => Key.OemPeriod,
            "," => Key.OemComma,
            ";" => Key.OemSemicolon,
            _ => Key.None
        };

        if (mappedKey != Key.None)
        {
            return (uint)KeyInterop.VirtualKeyFromKey(mappedKey);
        }

        if (Enum.TryParse<Key>(keyName, true, out var key))
        {
            return (uint)KeyInterop.VirtualKeyFromKey(key);
        }

        return (uint)KeyInterop.VirtualKeyFromKey(Key.D);
    }

    public void Dispose()
    {
        Stop();
        _settingsService.SettingsChanged -= OnSettingsChanged;
    }
}
