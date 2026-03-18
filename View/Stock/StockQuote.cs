// =========================================================================
//    StockQuote.cs (c) 2012 Nikolay Moroshkin, http://www.moroshkin.com/
// =========================================================================

using System;
using System.Windows;
using System.Windows.Media;

namespace QScalp.View.StockSpace
{
  class StockQuote : DrawingVisual
  {
    // **********************************************************************

    public readonly int Price;

    // **********************************************************************

    readonly FormattedText cFtPrice;
    readonly FormattedText cFtSpread;

    readonly double width;

    readonly DrawingVisual dvVolume;
    readonly DrawingVisual dvPrice;

    readonly double vMaxTextWidth;

    readonly Point volumeOrigin;
    readonly Point priceOrigin;

    QuoteType type;
    Brush brush;
    FormattedText ftPrice;

    Rect vFillRect;
    int volume;

    bool isBold;
    Typeface font;

    // **********************************************************************

    public StockQuote(int price)
    {
      this.Price = price;

      cFtSpread = new FormattedText(
        cfg.s.SpreadString,
        cfg.BaseCulture,
        FlowDirection.LeftToRight,
        cfg.BaseFont,
        cfg.u.FontSize,
        cfg.s.QuoteTextBrush);

      if(cfg.u.VQuotePriceWidth > cFtSpread.Width)
      {
        cFtPrice = new FormattedText(
          QScalp.Price.GetString(price),
          cfg.BaseCulture,
          FlowDirection.LeftToRight,
          cfg.BaseFont,
          cfg.u.FontSize,
          cfg.s.QuoteTextBrush);

        cFtPrice.MaxLineCount = 1;
        cFtPrice.TextAlignment = TextAlignment.Center;

        cFtSpread.MaxLineCount = 1;
        cFtSpread.TextAlignment = TextAlignment.Center;
      }
      else
      {
        cFtPrice = cFtSpread = new FormattedText(
          string.Empty,
          cfg.BaseCulture,
          FlowDirection.LeftToRight,
          cfg.BaseFont,
          cfg.u.FontSize,
          cfg.s.QuoteTextBrush);
      }

      width = Width;

      HGridLines.AddChildTo(Children, Price, width);
      Children.Add(dvVolume = new DrawingVisual());
      Children.Add(dvPrice = new DrawingVisual());

      vMaxTextWidth = cfg.u.VQuoteVolumeWidth - cfg.s.TextHMargin * 2;

      volumeOrigin = new Point(cfg.s.TextHMargin,
        cfg.QuoteHeight / 2 - cfg.TextTopOffset);

      priceOrigin = new Point(cfg.u.VQuoteVolumeWidth
        + cfg.s.VSplitter2Pen.Thickness + cfg.u.VQuotePriceWidth / 2,
        volumeOrigin.Y);

      vFillRect = new Rect(0, cfg.s.VolumeFillMargin, 0,
        cfg.QuoteHeight - cfg.s.VolumeFillMargin * 2);

      font = cfg.BaseFont;
    }

    // **********************************************************************

    public void Update(QuoteType type, int volume, bool isBold)
    {
      bool renderPrice = false;
      bool renderVolume = false;

      // ------------------------------------------------------------

      if(this.type != type)
      {
        this.type = type;

        Brush brush;
        FormattedText ftPrice;

        switch(type)
        {
          case QuoteType.Ask:
            ftPrice = cFtPrice;
            brush = cfg.s.AskQuoteBrush;
            break;

          case QuoteType.Bid:
            ftPrice = cFtPrice;
            brush = cfg.s.BidQuoteBrush;
            break;

          case QuoteType.BestAsk:
            ftPrice = cFtPrice;
            brush = cfg.s.BestAskQuoteBrush;
            break;

          case QuoteType.BestBid:
            ftPrice = cFtPrice;
            brush = cfg.s.BestBidQuoteBrush;
            break;

          case QuoteType.Spread:
            ftPrice = cFtSpread;
            brush = cfg.s.SpreadQuoteBrush;
            break;

          case QuoteType.Free:
            ftPrice = cFtPrice;
            brush = cfg.s.FreeQuoteBrush;
            break;

          default:
            ftPrice = null;
            brush = null;
            break;
        }

        if(this.brush != brush)
        {
          this.brush = brush;

          using(DrawingContext dc = this.RenderOpen())
            dc.DrawRectangle(brush, null, new Rect(0, 0, width, cfg.QuoteHeight));
        }

        if(this.ftPrice != ftPrice)
        {
          this.ftPrice = ftPrice;
          renderPrice = true;
        }
      }

      // ------------------------------------------------------------

      if(this.volume != volume)
      {
        this.volume = volume;
        renderVolume = true;
      }

      // ------------------------------------------------------------

      if(this.isBold != isBold)
      {
        this.isBold = isBold;

        font = isBold ? cfg.BoldFont : cfg.BaseFont;

        cFtPrice.SetFontTypeface(font);
        cFtSpread.SetFontTypeface(font);

        renderPrice = true;
        renderVolume = true;
      }

      // ------------------------------------------------------------

      if(renderPrice)
        using(DrawingContext dc = dvPrice.RenderOpen())
          dc.DrawText(ftPrice, priceOrigin);

      // ------------------------------------------------------------

      if(renderVolume)
      {
        if(volume > 0)
        {
          if(volume > cfg.u.FullVolume)
            vFillRect.Width = cfg.u.VQuoteVolumeWidth;
          else
            vFillRect.Width = Math.Round(volume * cfg.u.VQuoteVolumeWidth / cfg.u.FullVolume);

          using(DrawingContext dc = dvVolume.RenderOpen())
          {
            dc.DrawRectangle(cfg.s.VolumeFillBrush, null, vFillRect);

            if(vMaxTextWidth >= cfg.TextMinWidth)
            {
              FormattedText ftVolume = new FormattedText(
                volume.ToString("N", cfg.BaseCulture),
                cfg.BaseCulture,
                FlowDirection.LeftToRight,
                font,
                cfg.u.FontSize,
                cfg.s.QuoteTextBrush);

              ftVolume.MaxTextWidth = vMaxTextWidth;
              ftVolume.MaxLineCount = 1;

              dc.DrawText(ftVolume, volumeOrigin);
            }
          }
        }
        else
          using(DrawingContext dc = dvVolume.RenderOpen()) { }
      }

      // ------------------------------------------------------------
    }

    // **********************************************************************

    static public double Width
    {
      get
      {
        return cfg.u.VQuoteVolumeWidth
          + cfg.u.VQuotePriceWidth
          + cfg.s.VSplitter2Pen.Thickness;
      }
    }

    // **********************************************************************
  }
}
