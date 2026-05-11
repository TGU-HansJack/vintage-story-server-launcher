using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using System.Globalization;

namespace VSSL.Ui.Views.Dialogs;

/// <summary>
///     图片预览弹窗
/// </summary>
public class ImagePreviewWindow : Window
{
    private Bitmap? _bitmap;

    public ImagePreviewWindow(string title, string imagePath)
    {
        var isZh = CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);

        Title = title;
        Width = 980;
        Height = 720;
        MinWidth = 720;
        MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        Control contentControl;
        try
        {
            _bitmap = new Bitmap(imagePath);
            contentControl = new Image
            {
                Source = _bitmap,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }
        catch (Exception ex)
        {
            contentControl = new TextBlock
            {
                Text = isZh
                    ? $"无法加载图片：{ex.Message}"
                    : $"Failed to load image: {ex.Message}",
                TextWrapping = TextWrapping.Wrap
            };
        }

        var pathText = new TextBlock
        {
            Text = imagePath,
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.75
        };

        var viewer = new ScrollViewer
        {
            Content = contentControl,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        var closeButton = new Button
        {
            Content = isZh ? "关闭" : "Close",
            MinWidth = 100
        };
        closeButton.Click += (_, _) => Close(null);

        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children =
            {
                closeButton
            }
        };

        var root = new Grid
        {
            Margin = new Thickness(12),
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            RowSpacing = 10
        };
        root.Children.Add(pathText);
        Grid.SetRow(viewer, 1);
        root.Children.Add(viewer);
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);

        Content = root;
        Closed += (_, _) => _bitmap?.Dispose();
    }
}
