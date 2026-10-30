using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace learn_Assist.Converters;

/// <summary>
/// Convierte un valor enum en <see langword="true"/> si coincide con
/// el <see cref="ConverterParameter"/> (nombre del miembro del enum).
/// Útil para mostrar u ocultar controles según el <see cref="QuestionType"/>.
/// </summary>
public class EnumToBooleanConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Enum enumValue && parameter is string param)
            return string.Equals(enumValue.ToString(), param, StringComparison.OrdinalIgnoreCase);
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
