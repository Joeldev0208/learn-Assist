using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using learn_Assist.Models;

namespace learn_Assist.Converters;

public class MessageRoleToAlignmentConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is MessageRole role)
            return role == MessageRole.User ? Avalonia.Layout.HorizontalAlignment.Right : Avalonia.Layout.HorizontalAlignment.Left;
        return Avalonia.Layout.HorizontalAlignment.Left;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class MessageRoleToBubbleColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value is MessageRole role && role == MessageRole.User
            ? "BubbleUserBrush"
            : "BubbleAssistantBrush";

        if (Application.Current is { } app &&
            app.Resources.TryGetResource(key, app.RequestedThemeVariant, out var resource) &&
            resource is IBrush brush)
            return brush;

        return value is MessageRole r2 && r2 == MessageRole.User ? "#E3F2FD" : "#FFFFFF";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
