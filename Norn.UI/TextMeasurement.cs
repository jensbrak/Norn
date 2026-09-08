using System.Globalization;
using Avalonia.Controls;
using Avalonia.Media;

namespace Norn.UI;

/// <summary>
/// Measures a string's natural (unclipped) rendered width using a reference
/// control's own font. Shared by <see cref="StatusBar"/>'s path-compaction
/// and <see cref="MainWindow"/>'s clipped-name tooltip — both need to compare
/// a string's natural width against an actual arranged width.
/// </summary>
internal static class TextMeasurement
{
    public static double Width(TextBlock reference, string text)
        => new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(reference.FontFamily), reference.FontSize, Brushes.Black).Width;
}
