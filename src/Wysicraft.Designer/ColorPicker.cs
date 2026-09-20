using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
namespace Wysicraft.Designer;

/// <summary>HSV wheel with synchronized brightness, alpha, and editable ARGB hex.</summary>
public sealed class ColorPicker : Window
{
    const int Diameter = 200;
    double hue, saturation, brightness = 1, alpha = 1;
    bool syncing;
    readonly TextBox hex = new();
    readonly Slider value = new() { Minimum = 0, Maximum = 1, Value = 1 };
    readonly Slider opacity = new() { Minimum = 0, Maximum = 1, Value = 1 };
    readonly Border swatch = new() { Height = 35, Margin = new Thickness(0, 8, 0, 8) };
    readonly Canvas wheel = new() { Width = Diameter, Height = Diameter, Background = Brushes.Transparent };
    readonly Ellipse marker = new() { Width = 12, Height = 12, Stroke = Brushes.White, StrokeThickness = 2, Fill = Brushes.Transparent, IsHitTestVisible = false };
    readonly System.Action<string> changed;
    public ColorPicker(Window owner, string initial, System.Action<string> changed)
    {
        Owner = owner; Title = "Color selector"; Width = 300; Height = 475; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; this.changed = changed;
        var panel = new StackPanel { Margin = new Thickness(16) }; Content = panel;
        panel.Children.Add(wheel); wheel.Children.Add(new Image { Source = MakeWheel(), Width = Diameter, Height = Diameter, IsHitTestVisible = false }); wheel.Children.Add(marker);
        panel.Children.Add(new TextBlock { Text = "Brightness", Margin = new Thickness(0, 12, 0, 0) }); panel.Children.Add(value);
        panel.Children.Add(new TextBlock { Text = "Opacity" }); panel.Children.Add(opacity); panel.Children.Add(swatch);
        panel.Children.Add(new TextBlock { Text = "Hex: #RRGGBB or #AARRGGBB" }); panel.Children.Add(hex);
        var done = new Button { Content = "Done", IsDefault = true }; done.Click += (_, _) => Close(); panel.Children.Add(done);
        wheel.MouseLeftButtonDown += (_, e) => { wheel.CaptureMouse(); Pick(e.GetPosition(wheel)); };
        wheel.MouseMove += (_, e) => { if (e.LeftButton == MouseButtonState.Pressed && wheel.IsMouseCaptured) Pick(e.GetPosition(wheel)); };
        wheel.MouseLeftButtonUp += (_, _) => wheel.ReleaseMouseCapture();
        value.ValueChanged += (_, _) => { if (!syncing) { brightness = value.Value; Refresh(true); } };
        opacity.ValueChanged += (_, _) => { if (!syncing) { alpha = opacity.Value; Refresh(true); } };
        hex.TextChanged += (_, _) =>
        {
            if (syncing) return;
            if (!TryColor(hex.Text, out var color)) { hex.BorderBrush = Brushes.IndianRed; return; }
            FromColor(color); Refresh(false); changed(ToHex(color));
        };
        FromColor(TryColor(initial, out var start) ? start : Colors.White); Refresh(false);
    }
    void Pick(Point point)
    {
        double x = point.X - Diameter / 2d, y = point.Y - Diameter / 2d;
        hue = (Math.Atan2(y, x) * 180 / Math.PI + 360) % 360;
        saturation = Math.Min(1, Math.Sqrt(x * x + y * y) / (Diameter / 2d)); Refresh(true);
    }
    void FromColor(Color color)
    {
        double r = color.R / 255d, g = color.G / 255d, b = color.B / 255d;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), delta = max - min;
        hue = delta == 0 ? 0 : max == r ? 60 * (((g - b) / delta + 6) % 6) : max == g ? 60 * ((b - r) / delta + 2) : 60 * ((r - g) / delta + 4);
        saturation = max == 0 ? 0 : delta / max; brightness = max; alpha = color.A / 255d;
    }
    void Refresh(bool notify)
    {
        syncing = true;
        var color = Hsv(hue, saturation, brightness, alpha);
        hex.Text = ToHex(color); hex.BorderBrush = Brushes.Gray; swatch.Background = new SolidColorBrush(color);
        value.Value = brightness; opacity.Value = alpha;
        Canvas.SetLeft(marker, Diameter / 2d + Math.Cos(hue * Math.PI / 180) * saturation * Diameter / 2d - 6);
        Canvas.SetTop(marker, Diameter / 2d + Math.Sin(hue * Math.PI / 180) * saturation * Diameter / 2d - 6);
        syncing = false; if (notify) changed(ToHex(color));
    }
    public static bool TryColor(string text, out Color color)
    {
        color = Colors.White;
        if (!System.Text.RegularExpressions.Regex.IsMatch(text ?? "", "^#[0-9a-fA-F]{6}([0-9a-fA-F]{2})?$")) return false;
        color = (Color)ColorConverter.ConvertFromString(text)!; return true;
    }
    public static string ToHex(Color c) => c.A == 255 ? $"#{c.R:X2}{c.G:X2}{c.B:X2}" : $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
    static Color Hsv(double h, double s, double v, double a)
    {
        double c = v * s, x = c * (1 - Math.Abs(h / 60 % 2 - 1)), m = v - c;
        (double r, double g, double b) = h switch { < 60 => (c, x, 0d), < 120 => (x, c, 0d), < 180 => (0d, c, x), < 240 => (0d, x, c), < 300 => (x, 0d, c), _ => (c, 0d, x) };
        return Color.FromArgb((byte)Math.Round(a * 255), (byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
    }
    static BitmapSource MakeWheel()
    {
        byte[] pixels = new byte[Diameter * Diameter * 4];
        for (int y = 0; y < Diameter; y++) for (int x = 0; x < Diameter; x++)
        {
            double dx = x - Diameter / 2d, dy = y - Diameter / 2d, s = Math.Sqrt(dx * dx + dy * dy) / (Diameter / 2d);
            if (s > 1) continue; var c = Hsv((Math.Atan2(dy, dx) * 180 / Math.PI + 360) % 360, s, 1, 1); int i = (y * Diameter + x) * 4;
            pixels[i] = c.B; pixels[i + 1] = c.G; pixels[i + 2] = c.R; pixels[i + 3] = 255;
        }
        var bitmap = BitmapSource.Create(Diameter, Diameter, 96, 96, PixelFormats.Bgra32, null, pixels, Diameter * 4); bitmap.Freeze(); return bitmap;
    }
}
