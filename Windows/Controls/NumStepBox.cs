// =========================================================================
//    NumStepBox.cs (c) 2011 Nikolay Moroshkin, http://www.moroshkin.com/
// =========================================================================

using System;
using System.Windows;

namespace QScalp.Windows.Controls
{
  public partial class NumStepBox : NumUpDownBase
  {
    // **********************************************************************

    public NumStepBox() { Value = 1; }

    // **********************************************************************

    public int Ratio { get; protected set; }
    public int Step { get; protected set; }

    // **********************************************************************

    public void Set(int ratio, int step) { Value = (double)step / ratio; }

    // **********************************************************************

    public static double MaxValue = Math.Pow(10, (int)Math.Log10(int.MaxValue));
    public static double MinValue = 1 / MaxValue;

    // **********************************************************************

    public override double Value
    {
      get { return RawValue; }
      set
      {
        if(value <= 0)
          value = 1;
        else if(value < MinValue)
          value = MinValue;
        else if(value > MaxValue)
          value = MaxValue;

        int precision = (int)Math.Ceiling(-Math.Log10(value));

        if(precision > 0)
          DecimalPlaces = precision;
        else
          DecimalPlaces = 0;

        Ratio = (int)Math.Round(Math.Pow(10, DecimalPlaces));
        Step = (int)Math.Round(value * Ratio);

        RawValue = (double)Step / Ratio;
      }
    }

    // **********************************************************************

    protected override void ButtonUpClick(object sender, RoutedEventArgs e)
    {
      ValidateText();

      if(Value >= 1)
        Value++;
      else
      {
        if(Step >= 5)
          Value = (double)1 / Ratio * 10;
        else if(Step >= 2)
          Value = (double)5 / Ratio;
        else
          Value = (double)2 / Ratio;
      }
    }

    // **********************************************************************

    protected override void ButtonDownClick(object sender, RoutedEventArgs e)
    {
      ValidateText();

      if(Value > 1)
        Value--;
      else
      {
        if(Step <= 1)
          Value = (double)5 / Ratio / 10;
        else if(Step <= 2)
          Value = (double)1 / Ratio;
        else if(Step <= 5)
          Value = (double)2 / Ratio;
        else
          Value = (double)5 / Ratio;
      }
    }

    // **********************************************************************
  }
}
