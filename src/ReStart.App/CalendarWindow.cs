using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ReStart.App;

internal sealed class CalendarWindow : Window
{
    private DateTime _month = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private readonly StackPanel _body = new() { Margin = new(24) };
    public CalendarWindow(Window owner)
    {
        Owner = owner; Title = L.T("カレンダー"); Width = 400; Height = 450;
        ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false; Style = (Style)FindResource(typeof(Window)); ThemeWindow.Attach(this);
        Content = _body; Render();
    }
    private void Render()
    {
        _body.Children.Clear();
        _body.Children.Add(new TextBlock { Text = DateTime.Today.ToString("D", L.Culture), FontSize = 16, Margin = new(0, 0, 0, 18) });
        var nav = new DockPanel { Margin = new(0, 0, 0, 12) };
        var next = new Button { Content = "›", Width = 40 };
        var previous = new Button { Content = "‹", Width = 40 };
        DockPanel.SetDock(next, Dock.Right); DockPanel.SetDock(previous, Dock.Left);
        next.Click += (_, _) => { _month = _month.AddMonths(1); Render(); };
        previous.Click += (_, _) => { _month = _month.AddMonths(-1); Render(); };
        nav.Children.Add(next); nav.Children.Add(previous);
        nav.Children.Add(new TextBlock { Text = _month.ToString("Y", L.Culture), VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center });
        _body.Children.Add(nav);
        var days = new System.Windows.Controls.Primitives.UniformGrid { Columns = 7 };
        var firstDay = L.Culture.DateTimeFormat.FirstDayOfWeek;
        for (var i = 0; i < 7; i++) days.Children.Add(new TextBlock { Text = L.Culture.DateTimeFormat.AbbreviatedDayNames[((int)firstDay + i) % 7], HorizontalAlignment = HorizontalAlignment.Center, Margin = new(0, 8, 0, 8) });
        var start = _month.AddDays(-((7 + (int)_month.DayOfWeek - (int)firstDay) % 7));
        for (var i = 0; i < 42; i++)
        {
            var day = start.AddDays(i);
            var button = new Button { Content = day.Day.ToString(L.Culture), Height = 40, Padding = new(0), HorizontalContentAlignment = HorizontalAlignment.Center, Opacity = day.Month == _month.Month ? 1 : 0.4, ToolTip = day.ToString("D", L.Culture) };
            if (day == DateTime.Today) { button.SetResourceReference(BackgroundProperty, "AccentBrush"); button.Foreground = Brushes.White; }
            days.Children.Add(button);
        }
        _body.Children.Add(days);
    }
}
