using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using VisualNovelDetect.ViewModels;

namespace VisualNovelDetect.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;
    private readonly IServiceProvider _serviceProvider;

    public SettingsWindow(SettingsViewModel viewModel, IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _serviceProvider = serviceProvider;
        DataContext = _viewModel;

        _viewModel.RequestClose += Close;
        _viewModel.RequestSelectRegion += OnRequestSelectRegion;
    }

    private void ShortcutRecorder_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            ToggleRecording();
            e.Handled = true;
        }
    }

    private void RecordButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleRecording();
    }

    private void ToggleRecording()
    {
        if (_viewModel.IsRecordingShortcut)
        {
            StopRecording(cancelled: true);
        }
        else
        {
            StartRecording();
        }
    }

    private void StartRecording()
    {
        _viewModel.StartShortcutRecording();
        BtnRecordToggle.Content = "✕ Cancel";
        ShortcutRecorderBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(250, 204, 21)); // Glowing yellow
        ShortcutRecorderBorder.Background = new SolidColorBrush(Color.FromRgb(30, 41, 59));
        ShortcutRecorderBorder.Focus();
        Keyboard.Focus(ShortcutRecorderBorder);
    }

    private void StopRecording(bool cancelled)
    {
        if (cancelled)
        {
            _viewModel.CancelShortcutRecording();
        }

        BtnRecordToggle.Content = "🔴 Record";
        ShortcutRecorderBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248)); // Cyan
        ShortcutRecorderBorder.Background = new SolidColorBrush(Color.FromRgb(15, 23, 42));
        Keyboard.ClearFocus();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_viewModel.IsRecordingShortcut) return;

        // Escape cancels recording
        if (e.Key == Key.Escape)
        {
            StopRecording(cancelled: true);
            e.Handled = true;
            return;
        }

        // Get effective key (handling Alt which WPF delivers as SystemKey)
        Key key = (e.Key == Key.System) ? e.SystemKey : e.Key;

        // Extract modifier keys
        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
        bool alt = (Keyboard.Modifiers & ModifierKeys.Alt) != 0;

        // If the pressed key is solely a modifier key itself (Ctrl/Shift/Alt/Win), show real-time partial text
        if (IsModifierKey(key))
        {
            var partial = new StringBuilder();
            if (ctrl) partial.Append("Ctrl + ");
            if (alt) partial.Append("Alt + ");
            if (shift) partial.Append("Shift + ");
            partial.Append("...");
            _viewModel.UpdatePartialShortcut(partial.ToString());
            e.Handled = true;
            return;
        }

        // We have an actual trigger key pressed!
        string keyName = FormatKeyName(key);
        _viewModel.ApplyRecordedShortcut(ctrl, shift, alt, keyName);

        // Finish recording
        StopRecording(cancelled: false);
        e.Handled = true;
    }

    private void ResetShortcut_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ApplyRecordedShortcut(true, true, false, "D"); // Default: Ctrl + Shift + D
        StopRecording(cancelled: false);
    }

    private static bool IsModifierKey(Key key)
    {
        return key is Key.LeftCtrl or Key.RightCtrl
                   or Key.LeftAlt or Key.RightAlt
                   or Key.LeftShift or Key.RightShift
                   or Key.LWin or Key.RWin;
    }

    private static string FormatKeyName(Key key)
    {
        return key switch
        {
            Key.D0 => "0", Key.D1 => "1", Key.D2 => "2", Key.D3 => "3", Key.D4 => "4",
            Key.D5 => "5", Key.D6 => "6", Key.D7 => "7", Key.D8 => "8", Key.D9 => "9",
            Key.OemTilde => "`",
            Key.OemMinus => "-",
            Key.OemPlus => "+",
            Key.OemOpenBrackets => "[",
            Key.OemCloseBrackets => "]",
            Key.OemQuotes => "'",
            Key.OemQuestion => "/",
            Key.OemPeriod => ".",
            Key.OemComma => ",",
            Key.OemSemicolon => ";",
            _ => key.ToString()
        };
    }

    private void OnRequestSelectRegion()
    {
        var regionVm = _serviceProvider.GetRequiredService<RegionSelectorViewModel>();
        var regionWin = new RegionSelectorWindow(regionVm);
        regionWin.ShowDialog();
        _viewModel.LoadFromSettings();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Don't kill window, just hide it so it can be re-opened from tray
        e.Cancel = true;
        Hide();
    }
}
