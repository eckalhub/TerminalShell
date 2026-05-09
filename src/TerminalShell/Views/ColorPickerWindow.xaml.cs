using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using TerminalShell.Core;
using TerminalShell.Core.Config;
using TerminalShell.ViewModels;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;

namespace TerminalShell.Views;

public partial class ColorPickerWindow : Window
{
    private readonly ColorPickerViewModel _viewModel;

    public Color SelectedColor => _viewModel.CurrentColor;
    public string SelectedHex => _viewModel.Hex;

    public ColorPickerWindow(Color currentColor, Color defaultColor)
    {
        InitializeComponent();
        RuntimeAppIdentity.ApplyWindowIcon(this);

        _viewModel = new ColorPickerViewModel(ConfigManager.Instance, currentColor, defaultColor);
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        DataContext = _viewModel;

        Loaded += (_, _) => UpdateThumbsFromViewModel();
        Closed += (_, _) => _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ColorPickerViewModel.H)
            || e.PropertyName == nameof(ColorPickerViewModel.S)
            || e.PropertyName == nameof(ColorPickerViewModel.V))
        {
            UpdateThumbsFromViewModel();
        }
    }

    private void UpdateThumbsFromViewModel()
    {
        if (SpectrumCanvas.ActualWidth <= 0 || SpectrumCanvas.ActualHeight <= 0)
        {
            return;
        }

        double x = (_viewModel.H / 360.0) * SpectrumCanvas.ActualWidth;
        double y = (_viewModel.S / 100.0) * SpectrumCanvas.ActualHeight;

        Canvas.SetLeft(SpectrumThumb, x - 5);
        Canvas.SetTop(SpectrumThumb, y - 5);

        UpdateBrightnessThumbFromViewModel();
    }

    private void UpdateBrightnessThumbFromViewModel()
    {
        if (BrightnessCanvas.ActualHeight <= 0)
        {
            return;
        }

        double y = (1 - _viewModel.V / 100.0) * BrightnessCanvas.ActualHeight;
        Canvas.SetLeft(BrightnessThumb, 12);
        Canvas.SetTop(BrightnessThumb, y - 5);
    }

    private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is UIElement element && element.IsMouseCaptured)
        {
            element.ReleaseMouseCapture();
        }
    }

    private void SpectrumCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        if (_viewModel.V < 1.0)
        {
            _viewModel.V = 100.0;
        }

        Mouse.Capture(SpectrumCanvas);
        UpdateSpectrumFromMouse(e.GetPosition(SpectrumCanvas));
    }

    private void SpectrumCanvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && ReferenceEquals(Mouse.Captured, SpectrumCanvas))
        {
            UpdateSpectrumFromMouse(e.GetPosition(SpectrumCanvas));
        }
    }

    private void BrightnessCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        Mouse.Capture(BrightnessCanvas);
        UpdateBrightnessFromMouse(e.GetPosition(BrightnessCanvas));
    }

    private void BrightnessCanvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && ReferenceEquals(Mouse.Captured, BrightnessCanvas))
        {
            UpdateBrightnessFromMouse(e.GetPosition(BrightnessCanvas));
        }
    }

    private void UpdateBrightnessFromMouse(Point point)
    {
        double height = BrightnessCanvas.ActualHeight;
        if (height <= 0)
        {
            return;
        }

        double y = Math.Clamp(point.Y, 0, height);
        _viewModel.V = (1 - y / height) * 100.0;
    }

    private void UpdateSpectrumFromMouse(Point point)
    {
        double width = SpectrumCanvas.ActualWidth;
        double height = SpectrumCanvas.ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        double x = Math.Clamp(point.X, 0, width);
        double y = Math.Clamp(point.Y, 0, height);

        _viewModel.H = (x / width) * 360.0;
        _viewModel.S = (y / height) * 100.0;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        if (IsMouseCaptured)
        {
            Mouse.Capture(null);
        }
    }
}

public class ColorBrushConverter : IValueConverter
{
    public static ColorBrushConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Color color ? new SolidColorBrush(color) : System.Windows.Media.Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class ColorHexConverter : IValueConverter
{
    public static ColorHexConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Color color ? $"#{color.R:X2}{color.G:X2}{color.B:X2}" : ColorPaletteHelper.DefaultSlotColor;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BrightnessToOpacityConverter : IValueConverter
{
    public static BrightnessToOpacityConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is double brightness ? 1.0 - (brightness / 100.0) : 0.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
