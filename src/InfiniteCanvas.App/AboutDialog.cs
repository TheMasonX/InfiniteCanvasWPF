using System.Windows;
using System.Windows.Controls;

namespace InfiniteCanvas.App;

public sealed class AboutDialog : Window
{
    public AboutDialog()
    {
        Title = "About Infinite Canvas";
        Width = 520;
        Height = 360;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = SystemColors.WindowBrush;

        var content = new StackPanel
        {
            Margin = new Thickness(24)
        };

        content.Children.Add(new TextBlock
        {
            Text = "Infinite Canvas",
            FontSize = 24,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        });

        content.Children.Add(new TextBlock
        {
            Text = "A WPF-based inspection and spatial-indexing playground for exploring rendered scenes, annotations, and viewport behavior.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        });

        content.Children.Add(new TextBlock
        {
            Text = "Project attribution\n\nBuilt by Lucas Norr.\n\nThis project is distributed under the MIT License.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        });

        content.Children.Add(new TextBlock
        {
            Text = "Third-party credits\n\n- .NET and Windows Presentation Foundation\n- Serilog\n- CommunityToolkit.Mvvm\n- NetTopologySuite\n- Segoe UI Variable / Cascadia Mono",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        });

        var closeButton = new Button
        {
            Content = "Close",
            Width = 90,
            Height = 30,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0),
            IsDefault = true
        };
        closeButton.Click += (_, _) => Close();
        content.Children.Add(closeButton);

        Content = new ScrollViewer
        {
            Content = content,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
    }
}
