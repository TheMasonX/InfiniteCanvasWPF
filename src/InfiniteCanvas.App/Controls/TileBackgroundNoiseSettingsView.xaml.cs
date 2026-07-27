using System.Windows;
using System.Windows.Controls;
using InfiniteCanvas.ViewModels;

namespace InfiniteCanvas.App.Controls;

public partial class TileBackgroundNoiseSettingsView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(TileBackgroundNoiseSettingsViewModel),
            typeof(TileBackgroundNoiseSettingsView),
            new PropertyMetadata(null, OnViewModelChanged));

    public TileBackgroundNoiseSettingsView()
    {
        InitializeComponent();
    }

    public TileBackgroundNoiseSettingsViewModel ViewModel
    {
        get => (TileBackgroundNoiseSettingsViewModel)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TileBackgroundNoiseSettingsView control)
        {
            control.DataContext = e.NewValue;
        }
    }
}
