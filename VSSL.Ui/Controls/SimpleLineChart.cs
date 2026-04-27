using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace VSSL.Ui.Controls;

/// <summary>
///     简单折线图（Avalonia Canvas + DrawingContext）
/// </summary>
public class SimpleLineChart : Control
{
    public static readonly StyledProperty<IReadOnlyList<double>?> PrimaryValuesProperty =
        AvaloniaProperty.Register<SimpleLineChart, IReadOnlyList<double>?>(nameof(PrimaryValues));

    public static readonly StyledProperty<IReadOnlyList<double>?> SecondaryValuesProperty =
        AvaloniaProperty.Register<SimpleLineChart, IReadOnlyList<double>?>(nameof(SecondaryValues));

    public static readonly StyledProperty<IBrush?> PrimaryStrokeProperty =
        AvaloniaProperty.Register<SimpleLineChart, IBrush?>(nameof(PrimaryStroke), new SolidColorBrush(Color.Parse("#5B9BD5")));

    public static readonly StyledProperty<IBrush?> SecondaryStrokeProperty =
        AvaloniaProperty.Register<SimpleLineChart, IBrush?>(nameof(SecondaryStroke), new SolidColorBrush(Color.Parse("#ED7D31")));

    public static readonly StyledProperty<bool> ShowZeroBasedYAxisProperty =
        AvaloniaProperty.Register<SimpleLineChart, bool>(nameof(ShowZeroBasedYAxis), false);

    public static readonly StyledProperty<bool> IsDarkModeProperty =
        AvaloniaProperty.Register<SimpleLineChart, bool>(nameof(IsDarkMode), true);

    public static readonly StyledProperty<string> PrimaryLegendProperty =
        AvaloniaProperty.Register<SimpleLineChart, string>(nameof(PrimaryLegend), "系列 1");

    public static readonly StyledProperty<string> SecondaryLegendProperty =
        AvaloniaProperty.Register<SimpleLineChart, string>(nameof(SecondaryLegend), "系列 2");

    public static readonly StyledProperty<string> XAxisLabelProperty =
        AvaloniaProperty.Register<SimpleLineChart, string>(nameof(XAxisLabel), "采样点");

    public static readonly StyledProperty<string> YAxisLabelProperty =
        AvaloniaProperty.Register<SimpleLineChart, string>(nameof(YAxisLabel), string.Empty);

    public static readonly StyledProperty<string> YAxisValueFormatProperty =
        AvaloniaProperty.Register<SimpleLineChart, string>(nameof(YAxisValueFormat), "0.0");

    static SimpleLineChart()
    {
        AffectsRender<SimpleLineChart>(
            PrimaryValuesProperty,
            SecondaryValuesProperty,
            PrimaryStrokeProperty,
            SecondaryStrokeProperty,
            ShowZeroBasedYAxisProperty,
            IsDarkModeProperty,
            PrimaryLegendProperty,
            SecondaryLegendProperty,
            XAxisLabelProperty,
            YAxisLabelProperty,
            YAxisValueFormatProperty);
    }

    public IReadOnlyList<double>? PrimaryValues
    {
        get => GetValue(PrimaryValuesProperty);
        set => SetValue(PrimaryValuesProperty, value);
    }

    public IReadOnlyList<double>? SecondaryValues
    {
        get => GetValue(SecondaryValuesProperty);
        set => SetValue(SecondaryValuesProperty, value);
    }

    public IBrush? PrimaryStroke
    {
        get => GetValue(PrimaryStrokeProperty);
        set => SetValue(PrimaryStrokeProperty, value);
    }

    public IBrush? SecondaryStroke
    {
        get => GetValue(SecondaryStrokeProperty);
        set => SetValue(SecondaryStrokeProperty, value);
    }

    public bool ShowZeroBasedYAxis
    {
        get => GetValue(ShowZeroBasedYAxisProperty);
        set => SetValue(ShowZeroBasedYAxisProperty, value);
    }

    public bool IsDarkMode
    {
        get => GetValue(IsDarkModeProperty);
        set => SetValue(IsDarkModeProperty, value);
    }

    public string PrimaryLegend
    {
        get => GetValue(PrimaryLegendProperty);
        set => SetValue(PrimaryLegendProperty, value);
    }

    public string SecondaryLegend
    {
        get => GetValue(SecondaryLegendProperty);
        set => SetValue(SecondaryLegendProperty, value);
    }

    public string XAxisLabel
    {
        get => GetValue(XAxisLabelProperty);
        set => SetValue(XAxisLabelProperty, value);
    }

    public string YAxisLabel
    {
        get => GetValue(YAxisLabelProperty);
        set => SetValue(YAxisLabelProperty, value);
    }

    public string YAxisValueFormat
    {
        get => GetValue(YAxisValueFormatProperty);
        set => SetValue(YAxisValueFormatProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (Bounds.Width <= 30 || Bounds.Height <= 30) return;

        var hasSecondarySeries = SecondaryValues is { Count: > 0 };

        const double leftMargin = 52;
        const double rightMargin = 14;
        var topMargin = hasSecondarySeries ? 34d : 28d;
        const double bottomMargin = 34;
        var plotRect = new Rect(
            leftMargin,
            topMargin,
            Math.Max(0, Bounds.Width - leftMargin - rightMargin),
            Math.Max(0, Bounds.Height - topMargin - bottomMargin));
        if (plotRect.Width <= 10 || plotRect.Height <= 10) return;

        var gridColor = IsDarkMode
            ? Color.Parse("#26FFFFFF")
            : Color.Parse("#2A000000");
        var axisColor = IsDarkMode
            ? Color.Parse("#7AFFFFFF")
            : Color.Parse("#7A000000");
        var textBrush = new SolidColorBrush(IsDarkMode
            ? Color.Parse("#EDE6D8")
            : Color.Parse("#1F1F1F"));

        var gridPen = new Pen(new SolidColorBrush(gridColor), 1);
        var axisPen = new Pen(new SolidColorBrush(axisColor), 1);

        var allValues = CollectAllValues(PrimaryValues, SecondaryValues);
        var minValue = 0d;
        var maxValue = 1d;
        if (allValues.Count > 0)
        {
            minValue = allValues.Min();
            maxValue = allValues.Max();

            if (ShowZeroBasedYAxis) minValue = 0;
            if (Math.Abs(maxValue - minValue) < 0.0001)
                maxValue = minValue + 1;

            var rawRange = maxValue - minValue;
            var padding = rawRange * 0.08;
            if (ShowZeroBasedYAxis)
            {
                maxValue += padding;
            }
            else
            {
                minValue -= padding;
                maxValue += padding;
            }
        }

        DrawYAxis(context, plotRect, minValue, maxValue, gridPen, axisPen, textBrush);
        DrawXAxis(context, plotRect, axisPen, gridPen, textBrush, GetSampleCount());
        DrawAxisTitles(context, plotRect, textBrush);
        DrawLegend(context, textBrush);

        if (allValues.Count == 0) return;

        if (PrimaryValues is { Count: > 0 })
            DrawSeries(context, PrimaryValues, plotRect, minValue, maxValue, PrimaryStroke);

        if (SecondaryValues is { Count: > 0 })
            DrawSeries(context, SecondaryValues, plotRect, minValue, maxValue, SecondaryStroke);
    }

    private int GetSampleCount()
    {
        return Math.Max(
            PrimaryValues?.Count ?? 0,
            SecondaryValues?.Count ?? 0);
    }

    private static List<double> CollectAllValues(
        IReadOnlyList<double>? primary,
        IReadOnlyList<double>? secondary)
    {
        var values = new List<double>();

        if (primary is not null)
            values.AddRange(primary.Where(double.IsFinite));

        if (secondary is not null)
            values.AddRange(secondary.Where(double.IsFinite));

        return values;
    }

    private void DrawYAxis(
        DrawingContext context,
        Rect plotRect,
        double minValue,
        double maxValue,
        Pen gridPen,
        Pen axisPen,
        IBrush textBrush)
    {
        const int ticks = 5;
        for (var i = 0; i < ticks; i++)
        {
            var ratio = i / (double)(ticks - 1);
            var y = plotRect.Bottom - ratio * plotRect.Height;
            context.DrawLine(gridPen, new Point(plotRect.Left, y), new Point(plotRect.Right, y));
            context.DrawLine(axisPen, new Point(plotRect.Left - 4, y), new Point(plotRect.Left, y));

            var value = minValue + ratio * (maxValue - minValue);
            var label = FormatYAxisValue(value);
            DrawText(context, label, textBrush, new Point(plotRect.Left - 6, y - 7), HorizontalAnchor.Right, 11);
        }

        context.DrawLine(axisPen, new Point(plotRect.Left, plotRect.Top), new Point(plotRect.Left, plotRect.Bottom));
    }

    private void DrawXAxis(
        DrawingContext context,
        Rect plotRect,
        Pen axisPen,
        Pen gridPen,
        IBrush textBrush,
        int sampleCount)
    {
        context.DrawLine(axisPen, new Point(plotRect.Left, plotRect.Bottom), new Point(plotRect.Right, plotRect.Bottom));

        if (sampleCount <= 0) return;

        var indices = BuildTickIndices(sampleCount, 6);
        foreach (var index in indices)
        {
            var ratio = sampleCount == 1 ? 0 : index / (double)(sampleCount - 1);
            var x = plotRect.Left + ratio * plotRect.Width;

            context.DrawLine(gridPen, new Point(x, plotRect.Top), new Point(x, plotRect.Bottom));
            context.DrawLine(axisPen, new Point(x, plotRect.Bottom), new Point(x, plotRect.Bottom + 4));
            DrawText(
                context,
                (index + 1).ToString(CultureInfo.InvariantCulture),
                textBrush,
                new Point(x, plotRect.Bottom + 6),
                HorizontalAnchor.Center,
                11);
        }
    }

    private void DrawLegend(DrawingContext context, IBrush textBrush)
    {
        const double lineWidth = 16;
        const double lineTextGap = 4;
        const double itemGap = 10;
        const double fontSize = 12;
        const double rightPadding = 10;
        const double y = 6;

        var items = new List<(IBrush Brush, string Text)>();
        if (PrimaryValues is { Count: > 0 } && !string.IsNullOrWhiteSpace(PrimaryLegend))
            items.Add((PrimaryStroke ?? Brushes.SteelBlue, PrimaryLegend));
        if (SecondaryValues is { Count: > 0 } && !string.IsNullOrWhiteSpace(SecondaryLegend))
            items.Add((SecondaryStroke ?? Brushes.Orange, SecondaryLegend));

        if (items.Count == 0) return;

        var itemWidths = new List<double>(items.Count);
        var totalWidth = 0d;
        foreach (var item in items)
        {
            var width = lineWidth + lineTextGap + MeasureTextWidth(item.Text, textBrush, fontSize);
            itemWidths.Add(width);
            totalWidth += width;
        }

        totalWidth += itemGap * (items.Count - 1);
        var x = Math.Max(10, Bounds.Width - rightPadding - totalWidth);

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var pen = new Pen(item.Brush, 2);
            var lineY = y + 7;
            context.DrawLine(pen, new Point(x, lineY), new Point(x + lineWidth, lineY));

            DrawText(
                context,
                item.Text,
                textBrush,
                new Point(x + lineWidth + lineTextGap, y),
                HorizontalAnchor.Left,
                fontSize);

            x += itemWidths[i] + itemGap;
        }
    }

    private void DrawAxisTitles(DrawingContext context, Rect plotRect, IBrush textBrush)
    {
        if (!string.IsNullOrWhiteSpace(XAxisLabel))
            DrawText(
                context,
                XAxisLabel,
                textBrush,
                new Point(plotRect.Left + plotRect.Width / 2, Bounds.Height - 16),
                HorizontalAnchor.Center,
                11);

        if (!string.IsNullOrWhiteSpace(YAxisLabel))
            DrawText(
                context,
                YAxisLabel,
                textBrush,
                new Point(8, plotRect.Top - 16),
                HorizontalAnchor.Left,
                11);
    }

    private string FormatYAxisValue(double value)
    {
        var format = string.IsNullOrWhiteSpace(YAxisValueFormat) ? "0.##" : YAxisValueFormat;
        return value.ToString(format, CultureInfo.InvariantCulture);
    }

    private static IReadOnlyList<int> BuildTickIndices(int count, int maxTicks)
    {
        if (count <= 0) return Array.Empty<int>();
        if (count <= maxTicks) return Enumerable.Range(0, count).ToArray();

        var result = new List<int>(maxTicks);
        for (var i = 0; i < maxTicks; i++)
        {
            var ratio = i / (double)(maxTicks - 1);
            var index = (int)Math.Round(ratio * (count - 1));
            if (result.Count == 0 || result[^1] != index)
                result.Add(index);
        }

        if (result[^1] != count - 1)
            result[^1] = count - 1;
        return result;
    }

    private static double DrawText(
        DrawingContext context,
        string text,
        IBrush brush,
        Point origin,
        HorizontalAnchor anchor,
        double fontSize)
    {
        var formatted = new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            fontSize,
            brush);

        var x = origin.X;
        if (anchor == HorizontalAnchor.Center) x -= formatted.Width / 2;
        if (anchor == HorizontalAnchor.Right) x -= formatted.Width;

        context.DrawText(formatted, new Point(x, origin.Y));
        return formatted.Width;
    }

    private static double MeasureTextWidth(string text, IBrush brush, double fontSize)
    {
        var formatted = new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            fontSize,
            brush);

        return formatted.Width;
    }

    private static void DrawSeries(
        DrawingContext context,
        IReadOnlyList<double> values,
        Rect plotRect,
        double minValue,
        double maxValue,
        IBrush? strokeBrush)
    {
        if (values.Count == 0) return;

        var brush = strokeBrush ?? Brushes.SteelBlue;
        var pen = new Pen(brush, 2);
        var range = maxValue - minValue;

        if (values.Count == 1)
        {
            var y = ValueToY(values[0], plotRect, minValue, range);
            context.DrawEllipse(brush, null, new Point(plotRect.Left + plotRect.Width / 2, y), 2, 2);
            return;
        }

        Point? previous = null;
        for (var i = 0; i < values.Count; i++)
        {
            var x = plotRect.Left + plotRect.Width * i / (values.Count - 1d);
            var y = ValueToY(values[i], plotRect, minValue, range);
            var current = new Point(x, y);

            if (previous is not null)
                context.DrawLine(pen, previous.Value, current);

            previous = current;
        }
    }

    private static double ValueToY(double value, Rect plotRect, double minValue, double range)
    {
        var ratio = (value - minValue) / range;
        ratio = Math.Clamp(ratio, 0, 1);
        return plotRect.Bottom - ratio * plotRect.Height;
    }

    private enum HorizontalAnchor
    {
        Left,
        Center,
        Right
    }
}
