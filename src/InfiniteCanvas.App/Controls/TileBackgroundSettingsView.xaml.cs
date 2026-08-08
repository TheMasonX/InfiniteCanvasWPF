using System.Windows;
using System.Windows.Controls;
using InfiniteCanvas.ViewModels;

namespace InfiniteCanvas.App.Controls;

public partial class TileBackgroundSettingsView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(TileBackgroundSettingsViewModel),
            typeof(TileBackgroundSettingsView),
            new PropertyMetadata(null, OnViewModelChanged));

    public TileBackgroundSettingsView()
    {
        InitializeComponent();
    }

    public TileBackgroundSettingsViewModel ViewModel
    {
        get => (TileBackgroundSettingsViewModel)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TileBackgroundSettingsView control)
        {
            control.DataContext = e.NewValue;
        }
    }
}
