// =============================================================================
//  NumUpDownBase.xaml.cs (c) 2011 Nikolay Moroshkin, http://www.moroshkin.com/
// =============================================================================

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace QScalp.Windows.Controls
{
  public abstract partial class NumUpDownBase : UserControl
  {
    // **********************************************************************

    string format;

    public EventHandler ValueChanged { get; set; }

    // **********************************************************************

    public NumUpDownBase()
    {
      InitializeComponent();

      bb.Margin = new Thickness(
        tb.BorderThickness.Right + 1,
        tb.BorderThickness.Top + 1,
        tb.BorderThickness.Right + 1,
        tb.BorderThickness.Bottom + 1);

      Loaded += new RoutedEventHandler(NumUpDownBase_Loaded);

      decimalPlaces = -1;
      DecimalPlaces = 0;
    }

    // **********************************************************************

    void NumUpDownBase_Loaded(object sender, RoutedEventArgs e)
    {
      tb.Padding = new Thickness(
        tb.Padding.Left,
        tb.Padding.Top,
        bb.ActualWidth + tb.BorderThickness.Right * 2,
        tb.Padding.Bottom);
    }

    // **********************************************************************

    protected void UpdateText()
    {
      tb.Text = rawValue.ToString(format);
      tb.CaretIndex = tb.Text.Length;
    }

    // **********************************************************************

    protected void ValidateText()
    {
      double v;

      if(double.TryParse(tb.Text, out v) || tb.Text.Length == 0)
        Value = v;
      else
        UpdateText();
    }

    // **********************************************************************

    protected abstract void ButtonUpClick(object sender, RoutedEventArgs e);
    protected abstract void ButtonDownClick(object sender, RoutedEventArgs e);
    public abstract double Value { get; set; }

    // **********************************************************************

    double rawValue;
    protected double RawValue
    {
      get { return rawValue; }
      set
      {
        double v = Math.Round(value, DecimalPlaces);

        if(rawValue != v)
        {
          rawValue = v;

          if(ValueChanged != null)
            ValueChanged(this, new EventArgs());
        }

        UpdateText();
      }
    }

    // **********************************************************************

    int decimalPlaces;
    public int DecimalPlaces
    {
      get { return decimalPlaces; }
      set
      {
        if(decimalPlaces != value && value >= 0)
        {
          decimalPlaces = value;
          format = "#,##0." + "".PadRight(decimalPlaces, '0');

          UpdateText();
        }
      }
    }

    // **********************************************************************

    void tb_PreviewKeyDown(object sender, KeyEventArgs e)
    {
      switch(e.Key)
      {
        case Key.Return: ValidateText(); e.Handled = true; break;
        case Key.Up: ButtonUpClick(null, null); e.Handled = true; break;
        case Key.Down: ButtonDownClick(null, null); e.Handled = true; break;
      }
    }

    // **********************************************************************

    void tb_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
      ValidateText();
    }

    // **********************************************************************
  }
}
