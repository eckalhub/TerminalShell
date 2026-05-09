using System.Windows;

namespace TerminalShell.Controls;

public enum DropInsertionPlacement
{
    None,
    Before,
    After
}

public static class DropInsertionState
{
    public static readonly DependencyProperty PlacementProperty = DependencyProperty.RegisterAttached(
        "Placement",
        typeof(DropInsertionPlacement),
        typeof(DropInsertionState),
        new FrameworkPropertyMetadata(DropInsertionPlacement.None));

    public static void SetPlacement(DependencyObject element, DropInsertionPlacement value)
    {
        element.SetValue(PlacementProperty, value);
    }

    public static DropInsertionPlacement GetPlacement(DependencyObject element)
    {
        return (DropInsertionPlacement)element.GetValue(PlacementProperty);
    }
}
