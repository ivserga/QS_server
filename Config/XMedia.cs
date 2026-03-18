// =====================================================================
//    XMedia.cs (c) 2012 Nikolay Moroshkin, http://www.moroshkin.com/
// =====================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml;
using System.Xml.Serialization;

namespace QScalp.XMedia
{
  // ************************************************************************
  // *                              KeyBindings                             *
  // ************************************************************************

  public class KeyBindings : Dictionary<Key, OwnAction[]>, IXmlSerializable
  {
    // --------------------------------------------------------------

    const string cnBinding = "Binding";
    const string cnKey = "Key";
    const string cnQuote = "Quote";
    const string cnValue = "Value";
    const string cnQuantity = "Quantity";

    // --------------------------------------------------------------

    public KeyBindings() { }

    // --------------------------------------------------------------

    public KeyBindings(Key[] keys, OwnAction[][] actions)
    {
      if(keys.Length != actions.Length)
        throw new ArgumentException("Keys count != Actions count.");

      for(int i = 0; i < keys.Length; i++)
        this.Add(keys[i], actions[i]);
    }

    // --------------------------------------------------------------

    public System.Xml.Schema.XmlSchema GetSchema() { return null; }

    // --------------------------------------------------------------

    public void WriteXml(XmlWriter writer)
    {
      foreach(KeyValuePair<Key, OwnAction[]> kvp in this)
      {
        writer.WriteStartElement(cnBinding);
        writer.WriteAttributeString(cnKey, kvp.Key.ToString());

        foreach(OwnAction a in kvp.Value)
        {
          writer.WriteStartElement(a.Operation.ToString());

          if(a.Quote != BaseQuote.None)
            writer.WriteAttributeString(cnQuote, a.Quote.ToString());

          if(a.Value != 0)
          {
            writer.WriteStartAttribute(cnValue);
            writer.WriteValue(a.Value);
            writer.WriteEndAttribute();
          }

          if(a.Quantity != 0)
          {
            writer.WriteStartAttribute(cnQuantity);
            writer.WriteValue(a.Quantity);
            writer.WriteEndAttribute();
          }

          writer.WriteEndElement();
        }

        writer.WriteEndElement();
      }
    }

    // --------------------------------------------------------------

    public void ReadXml(XmlReader reader)
    {
      List<OwnAction> actions = new List<OwnAction>();

      if(!reader.IsEmptyElement)
        while(reader.Read() && reader.NodeType != XmlNodeType.EndElement)
          if(reader.NodeType == XmlNodeType.Element && reader.Name == cnBinding)
          {
            Key key = (Key)Enum.Parse(typeof(Key), reader.GetAttribute(cnKey));

            actions.Clear();

            if(!reader.IsEmptyElement)
              while(reader.Read() && reader.NodeType != XmlNodeType.EndElement)
                if(reader.IsEmptyElement)
                {
                  OwnAction a = new OwnAction((TradeOp)Enum.Parse(typeof(TradeOp), reader.Name));

                  if(reader.MoveToAttribute(cnQuote))
                    a.Quote = (BaseQuote)Enum.Parse(typeof(BaseQuote),
                      reader.ReadContentAsString());

                  if(reader.MoveToAttribute(cnValue))
                    a.Value = reader.ReadContentAsInt();

                  if(reader.MoveToAttribute(cnQuantity))
                    a.Quantity = reader.ReadContentAsInt();

                  actions.Add(a);
                }
                else
                  throw new FormatException();

            this.Add(key, actions.ToArray());
          }
          else
            throw new FormatException();

      reader.Read();
    }

    // --------------------------------------------------------------
  }


  // ************************************************************************
  // *                                 XBrush                               *
  // ************************************************************************

  public struct XBrush : IXmlSerializable
  {
    // --------------------------------------------------------------

    const string cnColor = "Color";

    SolidColorBrush brush;

    // --------------------------------------------------------------

    public static void Write(XmlWriter writer, SolidColorBrush brush)
    {
      Color c = brush.Color;

      writer.WriteAttributeString(cnColor,
        (c.A << 24 | c.R << 16 | c.G << 8 | c.B).ToString("x8"));
    }

    // --------------------------------------------------------------

    public static SolidColorBrush Read(XmlReader reader)
    {
      int c = int.Parse(
        reader.GetAttribute(cnColor),
        NumberStyles.HexNumber,
        NumberFormatInfo.InvariantInfo);

      unchecked
      {
        return new SolidColorBrush(Color.FromArgb(
          (byte)(c >> 24), (byte)(c >> 16), (byte)(c >> 8), (byte)(c)));
      }
    }

    // --------------------------------------------------------------

    public static implicit operator Brush(XBrush xb) { return xb.brush; }

    // --------------------------------------------------------------

    public Color Color { get { return brush.Color; } }

    // --------------------------------------------------------------

    public XBrush(Color color)
    {
      brush = new SolidColorBrush(color);
      brush.Freeze();
    }

    // --------------------------------------------------------------

    public SolidColorBrush Clone() { return brush.Clone(); }

    // --------------------------------------------------------------

    public System.Xml.Schema.XmlSchema GetSchema() { return null; }
    public void WriteXml(XmlWriter writer) { Write(writer, brush); }

    public void ReadXml(XmlReader reader)
    {
      brush = Read(reader);
      brush.Freeze();
    }

    // --------------------------------------------------------------
  }


  // ************************************************************************
  // *                                 XPen                                 *
  // ************************************************************************

  public struct XPen : IXmlSerializable
  {
    // --------------------------------------------------------------

    const string cnThickness = "Thickness";

    Pen pen;

    // --------------------------------------------------------------

    public static void Write(XmlWriter writer, Pen pen)
    {
      XBrush.Write(writer, (SolidColorBrush)pen.Brush);

      writer.WriteStartAttribute(cnThickness);
      writer.WriteValue(pen.Thickness);
      writer.WriteEndAttribute();
    }

    // --------------------------------------------------------------

    public static Pen Read(XmlReader reader)
    {
      Brush b = XBrush.Read(reader);

      if(reader.MoveToAttribute(cnThickness))
        return new Pen(b, reader.ReadContentAsDouble());
      else
        throw new FormatException();
    }

    // --------------------------------------------------------------

    public static implicit operator Pen(XPen xp) { return xp.pen; }

    // --------------------------------------------------------------

    public Brush Brush { get { return pen.Brush; } }
    public double Thickness { get { return pen.Thickness; } }

    // --------------------------------------------------------------

    public XPen(Color color, double thickness)
    {
      pen = new Pen(new SolidColorBrush(color), thickness);
      pen.Freeze();
    }

    // --------------------------------------------------------------

    public Pen Clone() { return pen.Clone(); }

    // --------------------------------------------------------------

    public System.Xml.Schema.XmlSchema GetSchema() { return null; }
    public void WriteXml(XmlWriter writer) { Write(writer, pen); }

    public void ReadXml(XmlReader reader)
    {
      pen = Read(reader);
      pen.Freeze();
    }

    // --------------------------------------------------------------
  }

  // ************************************************************************
}
