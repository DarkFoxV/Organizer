using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Organizer.Application.Views;

public sealed class ConfirmationDialog : Window
{
    private ConfirmationDialog(
        string title,
        string message,
        string primaryText,
        string cancelText,
        bool isDanger)
    {
        Title = title;
        Width = 420;
        MinWidth = 420;
        MaxWidth = 420;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brush.Parse("#161b22");

        var primaryButton = new Button
        {
            Content = primaryText,
            MinWidth = 96,
            Padding = new Avalonia.Thickness(14, 8),
            Background = Brush.Parse(isDanger ? "#dc2626" : "#2563eb"),
            Foreground = Brushes.White,
            BorderThickness = new Avalonia.Thickness(0),
            CornerRadius = new Avalonia.CornerRadius(8)
        };

        var cancelButton = new Button
        {
            Content = cancelText,
            MinWidth = 96,
            Padding = new Avalonia.Thickness(14, 8),
            Background = Brush.Parse("#1c2230"),
            Foreground = Brush.Parse("#e6edf3"),
            BorderBrush = Brush.Parse("#2a3347"),
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new Avalonia.CornerRadius(8)
        };

        primaryButton.Click += (_, _) => Close(true);
        cancelButton.Click += (_, _) => Close(false);

        Content = new Border
        {
            Padding = new Avalonia.Thickness(22),
            Child = new StackPanel
            {
                Spacing = 18,
                Children =
                {
                    new TextBlock
                    {
                        Text = title,
                        Foreground = Brush.Parse("#e6edf3"),
                        FontSize = 18,
                        FontWeight = FontWeight.SemiBold
                    },
                    new TextBlock
                    {
                        Text = message,
                        Foreground = Brush.Parse("#8b949e"),
                        FontSize = 13,
                        TextWrapping = TextWrapping.Wrap,
                        LineHeight = 20
                    },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 10,
                        Children =
                        {
                            cancelButton,
                            primaryButton
                        }
                    }
                }
            }
        };
    }

    public static Task<bool> ShowAsync(
        Window owner,
        string title,
        string message,
        string primaryText = "Continuar",
        string cancelText = "Cancelar",
        bool isDanger = false)
    {
        var dialog = new ConfirmationDialog(title, message, primaryText, cancelText, isDanger);
        return dialog.ShowDialog<bool>(owner);
    }
}
