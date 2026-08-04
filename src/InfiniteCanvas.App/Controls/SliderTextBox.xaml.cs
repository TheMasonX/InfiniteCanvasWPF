using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using InfiniteCanvas.Core;

namespace InfiniteCanvas.App.Controls;

/// <summary>
/// A label, a slider, and a numeric text box in one control.
/// </summary>
/// <remarks>
/// The text box keeps its own editing text and commits on Enter or focus loss.
/// Commits go through <see cref="BoundedNumeric"/> so the slider, the text box,
/// and the bound value always use one parse, clamp, and format path.
/// </remarks>
public partial class SliderTextBox : UserControl
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label),
        typeof(string),
        typeof(SliderTextBox),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
        nameof(Minimum),
        typeof(double),
        typeof(SliderTextBox),
        new PropertyMetadata(0d));

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum),
        typeof(double),
        typeof(SliderTextBox),
        new PropertyMetadata(100d));

    public static readonly DependencyProperty TickFrequencyProperty = DependencyProperty.Register(
        nameof(TickFrequency),
        typeof(double),
        typeof(SliderTextBox),
        new PropertyMetadata(1d));

    public static readonly DependencyProperty NumericTypeProperty = DependencyProperty.Register(
        nameof(NumericType),
        typeof(NumericKind),
        typeof(SliderTextBox),
        new PropertyMetadata(NumericKind.Double));

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value),
        typeof(double),
        typeof(SliderTextBox),
        new PropertyMetadata(0d, OnValueChanged));

    public static readonly DependencyProperty ShowSliderProperty = DependencyProperty.Register(
        nameof(ShowSlider),
        typeof(bool),
        typeof(SliderTextBox),
        new PropertyMetadata(true, OnShowSliderChanged));

    public SliderTextBox()
    {
        InitializeComponent();
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public double TickFrequency
    {
        get => (double)GetValue(TickFrequencyProperty);
        set => SetValue(TickFrequencyProperty, value);
    }

    public NumericKind NumericType
    {
        get => (NumericKind)GetValue(NumericTypeProperty);
        set => SetValue(NumericTypeProperty, value);
    }

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public bool ShowSlider
    {
        get => (bool)GetValue(ShowSliderProperty);
        set => SetValue(ShowSliderProperty, value);
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SliderTextBox control)
        {
            control.UpdateTextBoxFromValue((double)e.NewValue);
        }
    }

    private static void OnShowSliderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SliderTextBox control)
        {
            control.ApplyShowSlider((bool)e.NewValue);
        }
    }

    private void ApplyShowSlider(bool showSlider)
    {
        if (ValueSlider is not null)
        {
            ValueSlider.Visibility = showSlider ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void UpdateTextBoxFromValue(double value)
    {
        if (ValueTextBox is not null)
        {
            ValueTextBox.Text = BoundedNumeric.Format(value, NumericType);
        }
    }

    private void OnValueTextBoxGotFocus(object sender, RoutedEventArgs e)
    {
        ValueTextBox.SelectAll();
    }

    private void OnValueTextBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitTextBoxEdit();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            RevertTextBoxEdit();
            e.Handled = true;
        }
    }

    private void OnValueTextBoxLostFocus(object sender, RoutedEventArgs e)
    {
        CommitTextBoxEdit();
    }

    private void CommitTextBoxEdit()
    {
        if (BoundedNumeric.TryParse(ValueTextBox.Text, NumericType, Minimum, Maximum, out var parsed))
        {
            Value = parsed;
        }
        else
        {
            RevertTextBoxEdit();
        }
    }

    private void RevertTextBoxEdit()
    {
        ValueTextBox.Text = BoundedNumeric.Format(Value, NumericType);
    }
}
