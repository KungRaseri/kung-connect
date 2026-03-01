using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using KungConnect.Shared.Enums;

namespace KungConnect.Client.ViewModels;

/// <summary>
/// Converts a <see cref="MachineStatus"/> to a status-indicator colour.
/// Used in MachineListView via <c>vm:StatusToColorConverter.Instance</c>.
/// </summary>
public sealed class StatusToColorConverter : IValueConverter
{
    public static readonly StatusToColorConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is MachineStatus status
            ? status switch
            {
                MachineStatus.Online    => Colors.LimeGreen,
                MachineStatus.InSession => Colors.Orange,
                _                       => Colors.Gray
            }
            : Colors.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
