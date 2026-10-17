using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace DashboardMonitorBateraiManajemen.Converters
{
    /// <summary>
    /// Mengonversi nilai persen (0-100) dan jari-jari menjadi PathGeometry Busur Lingkaran (Arc)
    /// </summary>
    public class PercentageToArcConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 && values[0] != null && values[1] != null)
            {
                double percentage = ParseDouble(values[0]);
                double size = ParseDouble(values[1]);
                if (size <= 0) size = 110;
                double strokeThickness = values.Length > 2 ? ParseDouble(values[2]) : 8;
                if (strokeThickness <= 0) strokeThickness = 8;

                double radius = (size - strokeThickness) / 2;
                Point center = new Point(size / 2, size / 2);

                percentage = Math.Clamp(percentage, 0, 100);

                // Rentang sudut radial 260 derajat (-220° s/d +40°)
                double startAngle = -220.0;
                double totalAngle = 260.0;
                double sweepAngle = (percentage / 100.0) * totalAngle;
                if (sweepAngle < 0.2) sweepAngle = 0.2;

                double endAngle = startAngle + sweepAngle;

                Point startPoint = PointOnCircle(center, radius, startAngle);
                Point endPoint = PointOnCircle(center, radius, endAngle);

                bool isLargeArc = sweepAngle > 180.0;

                PathFigure figure = new PathFigure
                {
                    StartPoint = startPoint,
                    IsClosed = false
                };
                figure.Segments.Add(new ArcSegment
                {
                    Point = endPoint,
                    Size = new Size(radius, radius),
                    SweepDirection = SweepDirection.Clockwise,
                    IsLargeArc = isLargeArc
                });

                PathGeometry geometry = new PathGeometry();
                geometry.Figures.Add(figure);
                geometry.Freeze();
                return geometry;
            }

            return Geometry.Empty;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        private static Point PointOnCircle(Point center, double radius, double angleInDegrees)
        {
            double angleInRadians = angleInDegrees * (Math.PI / 180.0);
            return new Point(
                center.X + radius * Math.Cos(angleInRadians),
                center.Y + radius * Math.Sin(angleInRadians)
            );
        }

        private static double ParseDouble(object val)
        {
            if (val is double d) return d;
            if (val is float f) return f;
            if (val is int i) return i;
            if (val is string s && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed)) return parsed;
            try { return System.Convert.ToDouble(val, CultureInfo.InvariantCulture); } catch { return 0.0; }
        }
    }

    /// <summary>
    /// Menghitung persentase ke lebar/tinggi pengisian bar (Fill)
    /// </summary>
    public class PercentageToWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 && values[0] != null && values[1] != null)
            {
                double percentage = ParseDouble(values[0]);
                double totalLength = ParseDouble(values[1]);
                percentage = Math.Clamp(percentage, 0, 100);
                return Math.Max(0, (percentage / 100.0) * totalLength);
            }
            return 0.0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        private static double ParseDouble(object val)
        {
            if (val is double d) return d;
            if (val is float f) return f;
            if (val is int i) return i;
            if (val is string s && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed)) return parsed;
            try { return System.Convert.ToDouble(val, CultureInfo.InvariantCulture); } catch { return 0.0; }
        }
    }

    /// <summary>
    /// Format angka dengan desimal dan unit custom
    /// </summary>
    public class TelemetryFormatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double d)
            {
                string format = parameter as string ?? "F1";
                return d.ToString(format, CultureInfo.InvariantCulture);
            }
            return value?.ToString() ?? "0";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Invert boolean visibility
    /// </summary>
    public class InvertBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
            {
                return b ? Visibility.Collapsed : Visibility.Visible;
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Mengonversi persentase SOC (0 - 100) menjadi Brush dinamis:
    /// Hijau (Penuh > 60%) -> Kuning/Oranye (Sedang 20-60%) -> Merah (Kritis < 20%)
    /// </summary>
    public class SOCToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double soc = 50.0;
            if (value is double d) soc = d;
            else if (value is string s && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed)) soc = parsed;

            soc = Math.Clamp(soc, 0.0, 100.0);

            Color color;
            if (soc > 50.0)
            {
                // Interpolasi: 50% (Kuning #F59E0B: 245, 158, 11) -> 100% (Hijau #10B981: 16, 185, 129)
                double t = (soc - 50.0) / 50.0;
                byte r = (byte)(245 + (16 - 245) * t);
                byte g = (byte)(158 + (185 - 158) * t);
                byte b = (byte)(11 + (129 - 11) * t);
                color = Color.FromRgb(r, g, b);
            }
            else
            {
                // Interpolasi: 0% (Merah #EF4444: 239, 68, 68) -> 50% (Kuning #F59E0B: 245, 158, 11)
                double t = soc / 50.0;
                byte r = (byte)(239 + (245 - 239) * t);
                byte g = (byte)(68 + (158 - 68) * t);
                byte b = (byte)(68 + (11 - 68) * t);
                color = Color.FromRgb(r, g, b);
            }

            var gradient = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1)
            };
            gradient.GradientStops.Add(new GradientStop(Color.FromArgb(255, (byte)Math.Min(255, color.R + 30), (byte)Math.Min(255, color.G + 30), (byte)Math.Min(255, color.B + 30)), 0.0));
            gradient.GradientStops.Add(new GradientStop(color, 1.0));
            gradient.Freeze();
            return gradient;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
