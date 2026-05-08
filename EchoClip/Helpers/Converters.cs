using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using WpfBrush = System.Windows.Media.Brush;
using WpfSolidBrush = System.Windows.Media.SolidColorBrush;
using WpfColor = System.Windows.Media.Color;

namespace EchoClip.Helpers
{
    /// <summary>Returns Visibility.Visible when the bound value is non-null, Collapsed otherwise.</summary>
    [ValueConversion(typeof(object), typeof(Visibility))]
    public class NullToVisibilityConverter : IValueConverter
    {
        public static readonly NullToVisibilityConverter Instance = new();
        public object Convert(object? v, Type t, object? p, CultureInfo c) =>
            v != null ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object? v, Type t, object? p, CultureInfo c) =>
            throw new NotSupportedException();
    }

    /// <summary>Returns Visibility.Collapsed when bound value is non-null (inverse of above).</summary>
    [ValueConversion(typeof(object), typeof(Visibility))]
    public class NullToCollapsedConverter : IValueConverter
    {
        public static readonly NullToCollapsedConverter Instance = new();
        public object Convert(object? v, Type t, object? p, CultureInfo c) =>
            v == null ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object? v, Type t, object? p, CultureInfo c) =>
            throw new NotSupportedException();
    }

    /// <summary>Maps bool → Visible/Collapsed. Set ConverterParameter=Inverse to flip.</summary>
    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class BoolToVisibilityConverter : IValueConverter
    {
        public static readonly BoolToVisibilityConverter Instance  = new();
        public object Convert(object? v, Type t, object? p, CultureInfo c)
        {
            bool b = v is bool b2 && b2;
            bool inv = p is string s && s == "Inverse";
            return (b ^ inv) ? Visibility.Visible : Visibility.Collapsed;
        }
        public object ConvertBack(object? v, Type t, object? p, CultureInfo c) =>
            v is Visibility vis && vis == Visibility.Visible;
    }

    /// <summary>Maps bool → two brushes.</summary>
    [ValueConversion(typeof(bool), typeof(WpfBrush))]
    public class BoolToBrushConverter : IValueConverter
    {
        public WpfBrush TrueBrush  { get; set; } = new WpfSolidBrush(WpfColor.FromRgb(0x23, 0xA5, 0x59));
        public WpfBrush FalseBrush { get; set; } = new WpfSolidBrush(WpfColor.FromRgb(0x80, 0x84, 0x8E));

        public object Convert(object? v, Type t, object? p, CultureInfo c) =>
            v is bool b && b ? TrueBrush : FalseBrush;
        public object ConvertBack(object? v, Type t, object? p, CultureInfo c) =>
            throw new NotSupportedException();
    }

    /// <summary>
    /// Two-way: returns true when the bound string equals ConverterParameter.
    /// Used for radio-button groups bound to a single string property.
    /// </summary>
    [ValueConversion(typeof(string), typeof(bool))]
    public class StringEqualityConverter : IValueConverter
    {
        public object Convert(object? v, Type t, object? p, CultureInfo c) =>
            v?.ToString() == p?.ToString();

        public object ConvertBack(object? v, Type t, object? p, CultureInfo c) =>
            v is bool b && b ? p?.ToString() ?? string.Empty : Binding.DoNothing;
    }
}
