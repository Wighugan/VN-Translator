using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VisualNovelDetect.Native;
using VisualNovelDetect.ViewModels;
using Point = System.Windows.Point;

namespace VisualNovelDetect.Views;

public partial class RegionSelectorWindow : Window
{
    private readonly RegionSelectorViewModel _viewModel;
    private Point _startPoint;
    private bool _isDragging;
    private double _dpiScale = 1.0;

    public RegionSelectorWindow(RegionSelectorViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _viewModel.SelectionFinished += () => Dispatcher.Invoke(Close);

        // Position across virtual screen or current monitor
        var cursor = DpiHelper.GetCursorPhysicalPosition();
        var workArea = DpiHelper.GetMonitorWorkAreaDip((int)cursor.X, (int)cursor.Y, out _dpiScale);

        Left = workArea.Left;
        Top = workArea.Top;
        Width = workArea.Width;
        Height = workArea.Height;
    }

    private void Window_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            _startPoint = e.GetPosition(SelectionCanvas);
            _isDragging = true;

            Canvas.SetLeft(RubberBand, _startPoint.X);
            Canvas.SetTop(RubberBand, _startPoint.Y);
            RubberBand.Width = 0;
            RubberBand.Height = 0;
            RubberBand.Visibility = Visibility.Visible;
            CaptureMouse();
        }
    }

    private void Window_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDragging)
        {
            var curPoint = e.GetPosition(SelectionCanvas);
            double x = Math.Min(_startPoint.X, curPoint.X);
            double y = Math.Min(_startPoint.Y, curPoint.Y);
            double width = Math.Abs(curPoint.X - _startPoint.X);
            double height = Math.Abs(curPoint.Y - _startPoint.Y);

            Canvas.SetLeft(RubberBand, x);
            Canvas.SetTop(RubberBand, y);
            RubberBand.Width = width;
            RubberBand.Height = height;
        }
    }

    private async void Window_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            ReleaseMouseCapture();

            double dipLeft = Canvas.GetLeft(RubberBand) + Left;
            double dipTop = Canvas.GetTop(RubberBand) + Top;
            double dipWidth = RubberBand.Width;
            double dipHeight = RubberBand.Height;

            // Convert DIP to physical pixels
            int physX = (int)Math.Round(dipLeft * _dpiScale);
            int physY = (int)Math.Round(dipTop * _dpiScale);
            int physW = (int)Math.Round(dipWidth * _dpiScale);
            int physH = (int)Math.Round(dipHeight * _dpiScale);

            await _viewModel.SaveRegionAsync(physX, physY, physW, physH);
        }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _viewModel.Cancel();
        }
    }
}
