using System.Windows;
using System.Windows.Controls;
using TerminalShell.ViewModels;

namespace TerminalShell.Controls;

public partial class ThemeGroupPreviewControl : System.Windows.Controls.UserControl
{
    public ThemeGroupPreviewControl()
    {
        InitializeComponent();
    }

    public MainWindowThemeState? ThemeState
    {
        get => (MainWindowThemeState?)GetValue(ThemeStateProperty);
        set => SetValue(ThemeStateProperty, value);
    }

    public static readonly DependencyProperty ThemeStateProperty = DependencyProperty.Register(
        nameof(ThemeState),
        typeof(MainWindowThemeState),
        typeof(ThemeGroupPreviewControl),
        new PropertyMetadata(null));

    public ThemePreviewKind PreviewKind
    {
        get => (ThemePreviewKind)GetValue(PreviewKindProperty);
        set => SetValue(PreviewKindProperty, value);
    }

    public static readonly DependencyProperty PreviewKindProperty = DependencyProperty.Register(
        nameof(PreviewKind),
        typeof(ThemePreviewKind),
        typeof(ThemeGroupPreviewControl),
        new PropertyMetadata(ThemePreviewKind.WindowSession));
}
