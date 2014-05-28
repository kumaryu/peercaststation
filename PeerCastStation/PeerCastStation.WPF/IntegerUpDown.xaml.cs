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
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValuePropertyChanged, CoerceValue));

    private static object CoerceValue(DependencyObject d, object value)
    {
      var obj = (IntegerUpDown)d;
      return Math.Max(obj.Minimum, Math.Min(obj.Maximum, (int)value));
    }

    private static void OnValuePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs args)
    {
      ((IntegerUpDown)d).OnPropertyChanged((int)args.NewValue);
    }

    private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs args)
    {
      ((IntegerUpDown)d).OnPropertyChanged(((IntegerUpDown)d).Value);
    }

    private void OnPropertyChanged(int value)
    {
      if (value==Minimum && MinimumText!=null) {
        valueTextBox.Text = MinimumText;
      }
      else if (value==Maximum && MaximumText!=null) {
        valueTextBox.Text = MaximumText;
      }
      else {
        valueTextBox.Text = value.ToString();
      }
      upButton.IsEnabled   = (int)value<this.Maximum;
      downButton.IsEnabled = (int)value>this.Minimum;
    }

    public int Minimum {
      get { return (int)GetValue(MinimumProperty); }
      set { SetValue(MinimumProperty, value); }
    }
    public static readonly DependencyProperty MinimumProperty = 
      DependencyProperty.Register(
        "Minimum",
        typeof(int),
        typeof(IntegerUpDown),
        new PropertyMetadata(0, OnPropertyChanged));

    public string MinimumText {
      get { return (string)GetValue(MinimumTextProperty); }
      set { SetValue(MinimumTextProperty, value); }
    }
    public static readonly DependencyProperty MinimumTextProperty = 
      DependencyProperty.Register(
        "MinimumText",
        typeof(string),
        typeof(IntegerUpDown),
        new PropertyMetadata(null, OnPropertyChanged));

    public int Maximum {
      get { return (int)GetValue(MaximumProperty); }
      set { SetValue(MaximumProperty, value); }
    }
    public static readonly DependencyProperty MaximumProperty = 
      DependencyProperty.Register(
        "Maximum",
        typeof(int),
        typeof(IntegerUpDown),
        new PropertyMetadata(Int32.MaxValue, OnPropertyChanged));

    public string MaximumText {
      get { return (string)GetValue(MaximumTextProperty); }
      set { SetValue(MaximumTextProperty, value); }
    }
    public static readonly DependencyProperty MaximumTextProperty = 
      DependencyProperty.Register(
        "MaximumText",
        typeof(string),
        typeof(IntegerUpDown),
        new PropertyMetadata(null, OnPropertyChanged));

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
      else if (MinimumText!=null && valueTextBox.Text==MinimumText) {
        this.Value = this.Minimum;
      }
      else if (MaximumText!=null && valueTextBox.Text==MaximumText) {
        this.Value = this.Maximum;
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
