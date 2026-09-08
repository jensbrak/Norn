using Avalonia;
using Avalonia.Controls;

namespace Norn.UI;

/// <summary>
/// Constrains its single child to a fixed width:height ratio, centering it in
/// whatever space the parent offers. The child is handed the largest
/// ratio-correct rectangle that fits, so it tracks the window's size without
/// ever distorting.
/// </summary>
/// <remarks>
/// Avalonia has no built-in control for this. <c>Viewbox</c> scales a child
/// uniformly, but it still reports the full space it was offered, so a parent
/// has no way to know how much of that space the child actually covers.
/// Wrapping a <c>Viewbox</c> in this decorator gives both halves: a
/// ratio-correct outer box, and uniform scaling within it.
/// </remarks>
public sealed class AspectRatioBox : Decorator
{
    /// <summary>Width divided by height. Must be positive and finite.</summary>
    public static readonly StyledProperty<double> AspectRatioProperty =
        AvaloniaProperty.Register<AspectRatioBox, double>(
            nameof(AspectRatio),
            1d,
            validate: static ratio => ratio > 0 && double.IsFinite(ratio));

    static AspectRatioBox()
    {
        // Avalonia does not infer that a ratio change invalidates layout.
        AffectsMeasure<AspectRatioBox>(AspectRatioProperty);
    }

    public double AspectRatio
    {
        get => GetValue(AspectRatioProperty);
        set => SetValue(AspectRatioProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Child is null)
        {
            return default;
        }

        var constraint = LargestFitting(availableSize);
        Child.Measure(constraint);

        // One finite axis is enough to pin both, so the constraint is only
        // still infinite when the offer was unbounded on both — a child that
        // scrolls in both directions, say. Fall back to the smallest
        // ratio-correct box that covers what the child asked for, so that
        // degenerate case under-reports nothing and the child is never clipped.
        return double.IsInfinity(constraint.Width) || double.IsInfinity(constraint.Height)
            ? SmallestCovering(Child.DesiredSize)
            : constraint;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Child is null)
        {
            return finalSize;
        }

        // finalSize is always finite, so LargestFitting cannot return infinity.
        var child = LargestFitting(finalSize);
        var left = (finalSize.Width - child.Width) / 2;
        var top = (finalSize.Height - child.Height) / 2;

        Child.Arrange(new Rect(new Point(left, top), child));
        return finalSize;
    }

    /// <summary>Largest ratio-correct size that fits inside <paramref name="bounds"/>.</summary>
    private Size LargestFitting(Size bounds)
    {
        // Whichever axis runs out first decides; test the height-bound
        // candidate and fall back to the width-bound one.
        var widthIfHeightBound = bounds.Height * AspectRatio;

        return widthIfHeightBound <= bounds.Width
            ? new Size(widthIfHeightBound, bounds.Height)
            : new Size(bounds.Width, bounds.Width / AspectRatio);
    }

    /// <summary>Smallest ratio-correct size that fully contains <paramref name="content"/>.</summary>
    private Size SmallestCovering(Size content)
    {
        var width = Math.Max(content.Width, content.Height * AspectRatio);
        return new Size(width, width / AspectRatio);
    }
}
