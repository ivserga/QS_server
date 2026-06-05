// ==========================================================================
//  ClusterExportPeriodWindow.cs - Period selector for cluster export.
// ==========================================================================

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace QScalp.Windows
{
  sealed class ClusterExportPeriodWindow : Window
  {
    // **********************************************************************

    TextBox fromBox;
    TextBox toBox;

    public DateTime From { get; private set; }
    public DateTime To { get; private set; }

    // **********************************************************************

    public ClusterExportPeriodWindow(DateTime from, DateTime to)
    {
      Title = "Выгрузка кластеров для ИИ";
      ResizeMode = ResizeMode.NoResize;
      WindowStartupLocation = WindowStartupLocation.CenterOwner;
      SizeToContent = SizeToContent.WidthAndHeight;
      MinWidth = 420;

      From = from;
      To = to;

      Content = BuildContent();

      fromBox.Text = FormatDateTime(from);
      toBox.Text = FormatDateTime(to);
    }

    // **********************************************************************

    UIElement BuildContent()
    {
      Grid grid = new Grid();
      grid.Margin = new Thickness(12);

      grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
      grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

      for(int i = 0; i < 4; i++)
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

      TextBlock hint = new TextBlock();
      hint.Text = "Укажите период по времени начала кластера.";
      hint.Margin = new Thickness(0, 0, 0, 10);
      hint.TextWrapping = TextWrapping.Wrap;
      Grid.SetColumnSpan(hint, 2);
      grid.Children.Add(hint);

      AddLabel(grid, "From", 1);
      fromBox = AddTextBox(grid, 1);

      AddLabel(grid, "To", 2);
      toBox = AddTextBox(grid, 2);

      StackPanel buttons = new StackPanel();
      buttons.Orientation = Orientation.Horizontal;
      buttons.HorizontalAlignment = HorizontalAlignment.Right;
      buttons.Margin = new Thickness(0, 12, 0, 0);
      Grid.SetRow(buttons, 3);
      Grid.SetColumnSpan(buttons, 2);

      Button ok = new Button();
      ok.Content = "Экспорт";
      ok.MinWidth = 90;
      ok.Margin = new Thickness(0, 0, 8, 0);
      ok.IsDefault = true;
      ok.Click += OkClick;
      buttons.Children.Add(ok);

      Button cancel = new Button();
      cancel.Content = "Отмена";
      cancel.MinWidth = 90;
      cancel.IsCancel = true;
      buttons.Children.Add(cancel);

      grid.Children.Add(buttons);

      return grid;
    }

    // **********************************************************************

    void AddLabel(Grid grid, string text, int row)
    {
      TextBlock label = new TextBlock();
      label.Text = text;
      label.VerticalAlignment = VerticalAlignment.Center;
      label.Margin = new Thickness(0, 4, 8, 4);
      Grid.SetRow(label, row);
      grid.Children.Add(label);
    }

    TextBox AddTextBox(Grid grid, int row)
    {
      TextBox box = new TextBox();
      box.MinWidth = 280;
      box.Margin = new Thickness(0, 4, 0, 4);
      box.ToolTip = "Формат: yyyy-MM-dd HH:mm:ss";
      Grid.SetRow(box, row);
      Grid.SetColumn(box, 1);
      grid.Children.Add(box);
      return box;
    }

    // **********************************************************************

    void OkClick(object sender, RoutedEventArgs e)
    {
      DateTime from, to;
      if(!TryParseDateTime(fromBox.Text, out from))
      {
        MessageBox.Show(this, "Некорректное значение From. Формат: yyyy-MM-dd HH:mm:ss",
          cfg.ProgName, MessageBoxButton.OK, MessageBoxImage.Warning);
        fromBox.Focus();
        return;
      }

      if(!TryParseDateTime(toBox.Text, out to))
      {
        MessageBox.Show(this, "Некорректное значение To. Формат: yyyy-MM-dd HH:mm:ss",
          cfg.ProgName, MessageBoxButton.OK, MessageBoxImage.Warning);
        toBox.Focus();
        return;
      }

      if(from > to)
      {
        MessageBox.Show(this, "From должен быть меньше или равен To.",
          cfg.ProgName, MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
      }

      From = from;
      To = to;
      DialogResult = true;
    }

    // **********************************************************************

    static string FormatDateTime(DateTime value)
    {
      return value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }

    static bool TryParseDateTime(string text, out DateTime value)
    {
      string[] formats = new string[]
      {
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-dd HH:mm",
        "yyyy-MM-ddTHH:mm:ss",
        "dd.MM.yyyy HH:mm:ss",
        "dd.MM.yyyy HH:mm"
      };

      if(DateTime.TryParseExact(text, formats, CultureInfo.InvariantCulture,
        DateTimeStyles.AllowWhiteSpaces, out value))
        return true;

      return DateTime.TryParse(text, cfg.BaseCulture,
        DateTimeStyles.AllowWhiteSpaces, out value);
    }

    // **********************************************************************
  }
}
