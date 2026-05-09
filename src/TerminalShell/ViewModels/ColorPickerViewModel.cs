using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TerminalShell.Core;
using TerminalShell.Core.Config;
using TerminalShell.Models;
using Color = System.Windows.Media.Color;
using MediaColorConverter = System.Windows.Media.ColorConverter;

namespace TerminalShell.ViewModels;

public partial class ColorPickerViewModel : ObservableObject
{
    private readonly IConfigManager _configManager;
    private bool _isUpdating;
    private Color _currentColor;
    private Color _defaultColor;
    private Color _spectrumColor;
    private byte _r;
    private byte _g;
    private byte _b;
    private double _h;
    private double _s;
    private double _v;
    private string _hex = "#FF0000";

    public ObservableCollection<Color> ThemeColors { get; } = [];
    public ObservableCollection<Color> CustomColors { get; } = [];

    public IRelayCommand<Color> SetColorCommand { get; }
    public IRelayCommand AddCustomColorCommand { get; }

    public ColorPickerViewModel(IConfigManager configManager, Color initialColor, Color defaultColor)
    {
        _configManager = configManager;
        _defaultColor = defaultColor;

        InitializeCollections();
        LoadCustomColors();
        SetColorFromColor(initialColor);

        SetColorCommand = new RelayCommand<Color>(SetColorFromColor);
        AddCustomColorCommand = new RelayCommand(AddCurrentColorToCustomPalette);
    }

    public Color CurrentColor
    {
        get => _currentColor;
        set
        {
            if (SetProperty(ref _currentColor, value) && !_isUpdating)
            {
                HsvColor hsv = ColorHelper.RgbToHsv(value);
                UpdateAll(value.R, value.G, value.B, hsv.H, hsv.S, hsv.V, null);
            }

            OnPropertyChanged(nameof(CurrentColorBrush));
        }
    }

    public Color DefaultColor
    {
        get => _defaultColor;
        set => SetProperty(ref _defaultColor, value);
    }

    public byte R
    {
        get => _r;
        set
        {
            if (SetProperty(ref _r, value))
            {
                UpdateFromRgb();
            }
        }
    }

    public byte G
    {
        get => _g;
        set
        {
            if (SetProperty(ref _g, value))
            {
                UpdateFromRgb();
            }
        }
    }

    public byte B
    {
        get => _b;
        set
        {
            if (SetProperty(ref _b, value))
            {
                UpdateFromRgb();
            }
        }
    }

    public double H
    {
        get => _h;
        set
        {
            if (Math.Abs(_h - value) > 0.01)
            {
                _h = value;
                OnPropertyChanged();
                UpdateFromHsv();
                UpdateSpectrumColor();
            }
        }
    }

    public double S
    {
        get => _s;
        set
        {
            if (Math.Abs(_s - value) > 0.01)
            {
                _s = value;
                OnPropertyChanged();
                UpdateFromHsv();
            }
        }
    }

    public double V
    {
        get => _v;
        set
        {
            if (Math.Abs(_v - value) > 0.01)
            {
                _v = value;
                OnPropertyChanged();
                UpdateFromHsv();
            }
        }
    }

    public string Hex
    {
        get => _hex;
        set
        {
            if (SetProperty(ref _hex, value))
            {
                UpdateFromHex();
            }
        }
    }

    public Color SpectrumColor
    {
        get => _spectrumColor;
        set => SetProperty(ref _spectrumColor, value);
    }

    public SolidColorBrush CurrentColorBrush => new(CurrentColor);

    private void LoadCustomColors()
    {
        CustomColors.Clear();
        foreach (string color in ColorPaletteHelper.NormalizePalette(_configManager.Config.CustomColors))
        {
            CustomColors.Add(ColorHelper.HexToColor(color) ?? Colors.White);
        }
    }

    private void AddCurrentColorToCustomPalette()
    {
        if (CustomColors.Count == 0)
        {
            LoadCustomColors();
        }

        for (int i = CustomColors.Count - 1; i > 0; i--)
        {
            CustomColors[i] = CustomColors[i - 1];
        }

        if (CustomColors.Count > 0)
        {
            CustomColors[0] = CurrentColor;
        }

        _configManager.Config.CustomColors = ColorPaletteHelper.NormalizePalette(
            CustomColors.Select(ColorHelper.ColorToHex));
        _configManager.Save();
    }

    private void InitializeCollections()
    {
        Color[] columnAnchors =
        [
            (Color)MediaColorConverter.ConvertFromString("#808080"),
            (Color)MediaColorConverter.ConvertFromString("#C00000"),
            (Color)MediaColorConverter.ConvertFromString("#FFC000"),
            (Color)MediaColorConverter.ConvertFromString("#FFFF00"),
            (Color)MediaColorConverter.ConvertFromString("#92D050"),
            (Color)MediaColorConverter.ConvertFromString("#00B050"),
            (Color)MediaColorConverter.ConvertFromString("#00B0F0"),
            (Color)MediaColorConverter.ConvertFromString("#0070C0"),
            (Color)MediaColorConverter.ConvertFromString("#7030A0"),
            (Color)MediaColorConverter.ConvertFromString("#44546A")
        ];

        const int anchorRow = 6;

        for (int row = 0; row < 12; row++)
        {
            for (int col = 0; col < 10; col++)
            {
                Color result;
                if (col == 0)
                {
                    result = Lerp(Colors.White, (Color)MediaColorConverter.ConvertFromString("#858585"), row / 11.0);
                }
                else if (col == 9)
                {
                    result = Lerp((Color)MediaColorConverter.ConvertFromString("#858585"), Colors.Black, row / 11.0);
                }
                else
                {
                    Color anchor = columnAnchors[col];
                    if (row == anchorRow)
                    {
                        result = anchor;
                    }
                    else if (row < anchorRow)
                    {
                        double factor = (anchorRow - row) / (double)(anchorRow + 1) * 0.85;
                        result = Lerp(anchor, Colors.White, factor);
                    }
                    else
                    {
                        double factor = (row - anchorRow) / (double)(12 - anchorRow) * 0.75;
                        result = Lerp(anchor, Colors.Black, factor);
                    }
                }

                ThemeColors.Add(result);
            }
        }
    }

    private static Color Lerp(Color from, Color to, double amount)
    {
        byte r = (byte)(from.R + (to.R - from.R) * amount);
        byte g = (byte)(from.G + (to.G - from.G) * amount);
        byte b = (byte)(from.B + (to.B - from.B) * amount);
        return Color.FromRgb(r, g, b);
    }

    private void UpdateAll(byte r, byte g, byte b, double h, double s, double v, string? hexSource)
    {
        _isUpdating = true;

        R = r;
        G = g;
        B = b;
        H = h;
        S = s * 100;
        V = v * 100;

        _hex = hexSource ?? ColorHelper.ColorToHex(Color.FromRgb(r, g, b));
        OnPropertyChanged(nameof(Hex));

        _currentColor = Color.FromRgb(r, g, b);
        OnPropertyChanged(nameof(CurrentColor));
        OnPropertyChanged(nameof(CurrentColorBrush));

        UpdateSpectrumColor();
        _isUpdating = false;
    }

    private void SetColorFromColor(Color color)
    {
        if (_isUpdating)
        {
            return;
        }

        HsvColor hsv = ColorHelper.RgbToHsv(color);
        UpdateAll(color.R, color.G, color.B, hsv.H, hsv.S, hsv.V, null);
    }

    private void UpdateFromRgb()
    {
        if (_isUpdating)
        {
            return;
        }

        Color color = Color.FromRgb(R, G, B);
        HsvColor hsv = ColorHelper.RgbToHsv(color);
        UpdateAll(R, G, B, hsv.H, hsv.S, hsv.V, null);
    }

    private void UpdateFromHsv()
    {
        if (_isUpdating)
        {
            return;
        }

        Color color = ColorHelper.HsvToRgb(H, S / 100.0, V / 100.0);
        UpdateAll(color.R, color.G, color.B, H, S / 100.0, V / 100.0, null);
    }

    private void UpdateFromHex()
    {
        if (_isUpdating)
        {
            return;
        }

        Color? color = ColorHelper.HexToColor(Hex);
        if (color.HasValue)
        {
            HsvColor hsv = ColorHelper.RgbToHsv(color.Value);
            UpdateAll(color.Value.R, color.Value.G, color.Value.B, hsv.H, hsv.S, hsv.V, UiColorHelper.NormalizeColorString(Hex, ColorPaletteHelper.DefaultSlotColor));
        }
    }

    private void UpdateSpectrumColor()
    {
        SpectrumColor = ColorHelper.HsvToRgb(H, 1, 1);
    }
}
