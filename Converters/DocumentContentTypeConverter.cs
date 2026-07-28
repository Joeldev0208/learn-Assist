using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using learn_Assist.Models;

namespace learn_Assist.Converters;

public class ContentTypeToEmojiConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DocumentContentType type)
        {
            return type switch
            {
                DocumentContentType.Document => "📄",
                DocumentContentType.Image => "🖼",
                DocumentContentType.Video => "🎬",
                _ => "📄",
            };
        }
        if (value is string s)
        {
            return s.ToLower() switch
            {
                "pdf" => "📄",
                "figma" or "fig" => "🎨",
                "md" => "📝",
                "zip" => "📦",
                _ => "📄",
            };
        }
        return "📄";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
