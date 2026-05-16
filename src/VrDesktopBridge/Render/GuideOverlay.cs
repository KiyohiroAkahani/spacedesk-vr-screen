using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace VrDesktopBridge.Render;

/// <summary>
/// Builds the first-run on-screen guide labels (semi-transparent panel +
/// white text) as straight-alpha BGRA bitmaps, using WPF's offscreen
/// renderer (no extra deps; perfect alpha/anti-aliasing). Drawn once.
/// </summary>
public sealed class GuideLabel
{
    public byte[] Pixels { get; }
    public int Width { get; }
    public int Height { get; }

    private GuideLabel(byte[] px, int w, int h)
    {
        Pixels = px; Width = w; Height = h;
    }

    // spacedesk brand green (adjustable). Title black, hint green, frame green.
    private static readonly Color SpacedeskGreen = Color.FromRgb(0x3F, 0xB2, 0x3F);

    /// <summary>
    /// Render a two-line badge: <paramref name="title"/> (black) above
    /// <paramref name="hint"/> (spacedesk green), white panel, green frame.
    /// MUST be called on the UI thread.
    /// </summary>
    public static GuideLabel Build(string title, string hint)
    {
        var fam = new FontFamily("Segoe UI, Yu Gothic UI, Meiryo");
        var titleTb = new TextBlock
        {
            Text = title,
            Foreground = Brushes.Black,
            FontFamily = fam,
            FontSize = 110,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
        };
        var hintTb = new TextBlock
        {
            Text = hint,
            Foreground = new SolidColorBrush(SpacedeskGreen),
            FontFamily = fam,
            FontSize = 84,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 14, 0, 0),
        };
        var stack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        stack.Children.Add(titleTb);
        stack.Children.Add(hintTb);

        var border = new Border
        {
            Child = stack,
            Background = new SolidColorBrush(Color.FromArgb(245, 255, 255, 255)),
            BorderBrush = new SolidColorBrush(SpacedeskGreen),
            BorderThickness = new Thickness(6),
            CornerRadius = new CornerRadius(32),
            Padding = new Thickness(64, 36, 64, 36),
            SnapsToDevicePixels = true,
        };

        var inf = new Size(double.PositiveInfinity, double.PositiveInfinity);
        border.Measure(inf);
        var sz = border.DesiredSize;
        int w = Math.Max(1, (int)Math.Ceiling(sz.Width));
        int h = Math.Max(1, (int)Math.Ceiling(sz.Height));
        border.Arrange(new Rect(0, 0, w, h));
        border.UpdateLayout();

        var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(border);

        int stride = w * 4;
        var px = new byte[stride * h];
        rtb.CopyPixels(px, stride, 0);

        // RenderTargetBitmap is premultiplied (Pbgra32); our blend uses
        // straight alpha — un-premultiply.
        for (int i = 0; i < px.Length; i += 4)
        {
            byte a = px[i + 3];
            if (a is 0 or 255) continue;
            px[i + 0] = (byte)Math.Min(255, px[i + 0] * 255 / a);
            px[i + 1] = (byte)Math.Min(255, px[i + 1] * 255 / a);
            px[i + 2] = (byte)Math.Min(255, px[i + 2] * 255 / a);
        }
        return new GuideLabel(px, w, h);
    }
}
