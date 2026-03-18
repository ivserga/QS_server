// ========================================================================
//    NumUpDown.cs (c) 2011 Nikolay Moroshkin, http://www.moroshkin.com/
// ========================================================================

using System;
using System.Windows;

namespace QScalp.Windows.Controls
{
  public partial class NumUpDown : NumUpDownBase
  {
    // **********************************************************************

    public NumUpDown()
    {
      maxValue = Math.Pow(10, (int)Math.Log10(int.MaxValue));
      minValue = -maxValue;
      Increment = 1;
    }

    // **********************************************************************

    public override double Value
    {
      get { return RawValue; }
      set
      {
        if(value < MinValue)
          RawValue = minValue;
        else if(value > MaxValue)
          RawValue = maxValue;
        else
          RawValue = value;
      }
    }

    // **********************************************************************

    double minValue;
    public double MinValue
    {
      get { return minValue; }
      set
      {
        if(minValue != value)
        {
          minValue = value;
          Value = RawValue;
        }
      }
    }

    // **********************************************************************

    double maxValue;
    public double MaxValue
    {
      get { return maxValue; }
      set
      {
        if(maxValue != value)
        {
          maxValue = value;
          Value = RawValue;
        }
      }
    }

    // **********************************************************************

    public double Increment { get; set; }

    // **********************************************************************

    protected override void ButtonUpClick(object sender, RoutedEventArgs e)
    {
      ValidateText();
      Value += Increment;
    }

    // **********************************************************************

    protected override void ButtonDownClick(object sender, RoutedEventArgs e)
    {
      ValidateText();
      Value -= Increment;
    }

    // **********************************************************************
  }
}
