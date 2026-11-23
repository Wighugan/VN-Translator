using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using VisualNovelDetect.Native;
using VisualNovelDetect.ViewModels;
using Point = System.Windows.Point;

namespace VisualNovelDetect.Views;

public partial class PopupWindow : Window
{
    private readonly PopupViewModel _viewModel;

    // Moving state
    private bool _isMovingWindow;
    private Point _moveStartScreen;
    private double _startWindowLeft;
    private double _startWindowTop;
    private double _moveDpiScale = 1.0;

    // Resizing state
    private bool _isResizingWindow;
    private Point _resizeStartScreen;
    private double _startWindowWidth;
    private double _startWindowHeight;
    private double _resizeDpiScale = 1.0;

    public PopupWindow(PopupViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.RequestPositionUpdate += OnRequestPositionUpdate;

        // Hook mouse events for smooth move and resize even when dragging fast
        MoveHandle.PreviewMouseMove += MoveHandle_MouseMove;
        MoveHandle.PreviewMouseLeftButtonUp += MoveHandle_MouseUp;

        ResizeGrip.PreviewMouseMove += ResizeGrip_MouseMove;
        ResizeGrip.PreviewMouseLeftButtonUp += ResizeGrip_MouseUp;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // Crucial Win32 styles:
        // WS_EX_NOACTIVATE (0x08000000) prevents stealing keyboard/mouse focus from the Visual Novel
        // WS_EX_TOOLWINDOW (0x00000080) hides window from Alt-Tab task switcher
        var helper = new WindowInteropHelper(this);
        int exStyle = Win32.GetWindowLongPtr(helper.Handle, Win32.GWL_EXSTYLE).ToInt32();
        Win32.SetWindowLongPtr(
            helper.Handle,
            Win32.GWL_EXSTYLE,
            new IntPtr(exStyle | Win32.WS_EX_NOACTIVATE | Win32.WS_EX_TOOLWINDOW));
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PopupViewModel.IsVisible))
        {
            Dispatcher.Invoke(() =>
            {
                if (_viewModel.IsVisible)
                {
                    Left = _viewModel.WindowLeft;
                    Top = _viewModel.WindowTop;
                    Width = _viewModel.PopupWidth;
                    Height = _viewModel.PopupHeight;
                    Show();
                }
                else
                {
                    Hide();
                }
            });
        }
    }

    private void OnRequestPositionUpdate()
    {
        Dispatcher.Invoke(() =>
        {
            Left = _viewModel.WindowLeft;
            Top = _viewModel.WindowTop;
        });
    }

    #region Window Moving Logic (Move Handle)

    private void MoveHandle_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            _isMovingWindow = true;
            _moveStartScreen = DpiHelper.GetCursorPhysicalPosition();
            _moveDpiScale = DpiHelper.GetDpiScaleForPoint((int)_moveStartScreen.X, (int)_moveStartScreen.Y);
            if (_moveDpiScale <= 0) _moveDpiScale = 1.0;

            _startWindowLeft = Left;
            _startWindowTop = Top;

            _viewModel.IsMoving = true;
            _viewModel.PauseAutoCloseTimer();
            MoveHandle.CaptureMouse();
            e.Handled = true;
        }
    }

    private void MoveHandle_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isMovingWindow)
        {
            var curScreen = DpiHelper.GetCursorPhysicalPosition();
            double dx = (curScreen.X - _moveStartScreen.X) / _moveDpiScale;
            double dy = (curScreen.Y - _moveStartScreen.Y) / _moveDpiScale;

            Left = _startWindowLeft + dx;
            Top = _startWindowTop + dy;
            e.Handled = true;
        }
    }

    private void MoveHandle_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isMovingWindow)
        {
            _isMovingWindow = false;
            MoveHandle.ReleaseMouseCapture();

            _viewModel.IsMoving = false;
            _viewModel.UpdatePosition(Left, Top);
            _viewModel.ResetAutoCloseTimer();
            e.Handled = true;
        }
    }

    #endregion

    #region Window Resizing Logic (Resize Grip)

    private void ResizeGrip_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            _isResizingWindow = true;
            _resizeStartScreen = DpiHelper.GetCursorPhysicalPosition();
            _resizeDpiScale = DpiHelper.GetDpiScaleForPoint((int)_resizeStartScreen.X, (int)_resizeStartScreen.Y);
            if (_resizeDpiScale <= 0) _resizeDpiScale = 1.0;

            _startWindowWidth = ActualWidth > 0 ? ActualWidth : Width;
            _startWindowHeight = ActualHeight > 0 ? ActualHeight : Height;

            _viewModel.PauseAutoCloseTimer();
            ResizeGrip.CaptureMouse();
            e.Handled = true;
        }
    }

    private void ResizeGrip_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isResizingWindow)
        {
            var curScreen = DpiHelper.GetCursorPhysicalPosition();
            double dx = (curScreen.X - _resizeStartScreen.X) / _resizeDpiScale;
            double dy = (curScreen.Y - _resizeStartScreen.Y) / _resizeDpiScale;

            double newW = Math.Clamp(_startWindowWidth + dx, MinWidth, MaxWidth);
            double newH = Math.Clamp(_startWindowHeight + dy, MinHeight, MaxHeight);

            Width = newW;
            Height = newH;
            e.Handled = true;
        }
    }

    private void ResizeGrip_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isResizingWindow)
        {
            _isResizingWindow = false;
            ResizeGrip.ReleaseMouseCapture();

            _viewModel.UpdateSize(Width, Height);
            _viewModel.ResetAutoCloseTimer();
            e.Handled = true;
        }
    }

    #endregion

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _viewModel.Hide();
            e.Handled = true;
        }
    }

    private void Window_MouseEnter(object sender, MouseEventArgs e)
    {
        _viewModel.PauseAutoCloseTimer();
    }

    private void Window_MouseLeave(object sender, MouseEventArgs e)
    {
        if (!_isMovingWindow && !_isResizingWindow && !_viewModel.IsPinned)
        {
            _viewModel.ResetAutoCloseTimer();
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Don't close window, just hide it so it can be re-displayed with zero latency
        e.Cancel = true;
        _viewModel.Hide();
    }
}
