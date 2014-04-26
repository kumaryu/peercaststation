using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PeerCastStation.WPF
{
  /// <summary>
  /// IntegerUpDown.xaml の相互作用ロジック
  /// </summary>
  public partial class IntegerUpDown : UserControl
  {
    public int Value {
      get { return (int)GetValue(ValueProperty); }
      set { SetValue(ValueProperty, value); }
    }
    public static readonly DependencyProperty ValueProperty = 
      DependencyProperty.Register(
        "Value",
        typeof(int),
        typeof(IntegerUpDown),
        new PropertyMetadata(0, OnValuePropertyChanged, CoerceValue));

    private static object CoerceValue(DependencyObject d, object value)
    {
      var obj = (IntegerUpDown)d;
      return Math.Max(obj.Minimum, Math.Min(obj.Maximum, (int)value));
    }

    private static void OnValuePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs args)
    {
      ((IntegerUpDown)d).OnValueChanged(args);
    }

    private void OnValueChanged(DependencyPropertyChangedEventArgs args)
    {
      valueTextBox.Text = args.NewValue.ToString();
      upButton.IsEnabled   = (int)args.NewValue<this.Maximum;
      downButton.IsEnabled = (int)args.NewValue>this.Minimum;
    }

    public int Minimum {
      get { return (int)GetValue(MinimumProperty); }
      set { SetValue(MinimumProperty, value); }
    }
    public static readonly DependencyProperty MinimumProperty = 
      DependencyProperty.Register("Minimum", typeof(int), typeof(IntegerUpDown), new PropertyMetadata(0));

    public int Maximum {
      get { return (int)GetValue(MaximumProperty); }
      set { SetValue(MaximumProperty, value); }
    }
    public static readonly DependencyProperty MaximumProperty = 
      DependencyProperty.Register("Maximum", typeof(int), typeof(IntegerUpDown), new PropertyMetadata(Int32.MaxValue));

    public int Increment {
      get { return (int)GetValue(IncrementProperty); }
      set { SetValue(IncrementProperty, value); }
    }
    public static readonly DependencyProperty IncrementProperty = 
      DependencyProperty.Register("Increment", typeof(int), typeof(IntegerUpDown), new PropertyMetadata(1));

    public IntegerUpDown()
    {
      InitializeComponent();
    }

    private void valueTextBox_KeyDown(object sender, KeyEventArgs e)
    {
      switch (e.Key) {
      case Key.Enter:
        valueTextBox_Validate(sender, e);
        break;
      default:
        e.Handled = false;
        break;
      }
    }

    private void valueTextBox_Validate(object sender, RoutedEventArgs e)
    {
      int value;
      if (Int32.TryParse(valueTextBox.Text, out value)) {
        this.Value = Math.Max(this.Minimum, Math.Min(this.Maximum, value));
      }
      else {
        valueTextBox.Text = this.Value.ToString();
      }
    }

    private void upButton_Click(object sender, RoutedEventArgs e)
    {
      this.Value = Math.Min(this.Maximum, this.Value+this.Increment);
    }

    private void downButton_Click(object sender, RoutedEventArgs e)
    {
      this.Value = Math.Max(this.Minimum, this.Value-this.Increment);
    }

    private void valueTextBox_MouseWheel(object sender, MouseWheelEventArgs e)
    {
      if (e.Delta>0) upButton_Click(sender, e);
      if (e.Delta<0) downButton_Click(sender, e);
      e.Handled = true;
    }

    private void valueTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
      switch (e.Key) {
      case Key.Up:
        upButton_Click(sender, e);
        break;
      case Key.Down:
        downButton_Click(sender, e);
        break;
      default:
        e.Handled = false;
        break;
      }
    }
  }

}
