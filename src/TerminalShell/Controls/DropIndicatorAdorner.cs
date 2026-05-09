using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Point = System.Windows.Point;

namespace TerminalShell.Controls;

public sealed class DropIndicatorAdorner : Adorner
{
    private const double LineInsetLeft = 28;
    private const double LineInsetRight = 18;
    private const double IndicatorThickness = 2;
    private const double SlotHeight = 6;

    private readonly SolidColorBrush _indicatorBrush;
    private readonly System.Windows.Media.Pen _indicatorPen;

    public DropIndicatorAdorner(UIElement adornedElement, DropInsertionPlacement placement)
        : base(adornedElement)
    {
        Placement = placement;

        SolidColorBrush brush = new((Color)ColorConverter.ConvertFromString("#FFD700"));
        brush.Freeze();
        _indicatorBrush = brush;

        System.Windows.Media.Pen pen = new(_indicatorBrush, IndicatorThickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        pen.Freeze();
        _indicatorPen = pen;

        IsHitTestVisible = false;
        SnapsToDevicePixels = true;
        UseLayoutRounding = true;
    }

    public DropInsertionPlacement Placement { get; }

    protected override void OnRender(DrawingContext drawingContext)
    {
        if (Placement == DropInsertionPlacement.None)
        {
            return;
        }

        Rect adornedElementRect = new(AdornedElement.RenderSize);
        if (adornedElementRect.Width <= 0 || adornedElementRect.Height <= 0)
        {
            return;
        }

        double left = adornedElementRect.Left + LineInsetLeft;
        double right = adornedElementRect.Right - LineInsetRight;
        if (right <= left)
        {
            left = adornedElementRect.Left + 8;
            right = adornedElementRect.Right - 8;
            if (right <= left)
            {
                return;
            }
        }

        double y = Placement == DropInsertionPlacement.Before
            ? adornedElementRect.Top + (SlotHeight / 2)
            : adornedElementRect.Bottom - (SlotHeight / 2);

        drawingContext.PushGuidelineSet(new GuidelineSet(
            new[] { left, right },
            new[] { y }));

        drawingContext.DrawLine(_indicatorPen, new Point(left, y), new Point(right, y));

        drawingContext.Pop();
    }
}
