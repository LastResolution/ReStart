using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace ReStart.App.Tablet.Home;

/// <summary>
/// Asks for the Home Assistant address and a long-lived access token the user creates
/// (Profile → Security → Long-lived access tokens). Tests it, then stores it with DPAPI.
/// </summary>
internal sealed class HomeAssistantWindow : Window
{
    private readonly HomeAssistantClient _client;
    private readonly TextBox _url;
    private readonly PasswordBox _token = new() { MaxLength = 4096, Margin = new(0, 0, 0, 4), Padding = new(6, 8, 6, 8) };
    private readonly TextBlock _feedback = new() { TextWrapping = TextWrapping.Wrap, Margin = new(0, 12, 0, 0) };
    private readonly Button _save;
    private bool _saved;

    internal HomeAssistantWindow(Window owner, HomeAssistantClient client)
    {
        _client = client; Owner = owner; ShowInTaskbar = false; Title = "Home Assistant";
        Width = 520; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Style = (Style)Application.Current.FindResource(typeof(Window)); ThemeWindow.Attach(this);
        var body = new StackPanel { Margin = new(24) };
        body.Children.Add(new TextBlock { Text = "Connect Home Assistant", FontSize = 22, FontWeight = FontWeights.Light, Margin = new(0, 0, 0, 10) });
        body.Children.Add(new TextBlock
        {
            Text = "In Home Assistant, open your profile → Security → Long-lived access tokens → Create token, then paste it here. "
                + "ReStart keeps it encrypted for your Windows account (DPAPI) and only sends it to this address.",
            TextWrapping = TextWrapping.Wrap, Margin = new(0, 0, 0, 16)
        });
        body.Children.Add(new TextBlock { Text = "Address", Margin = new(0, 0, 0, 5) });
        _url = new TextBox { Text = client.Url ?? HomeAssistantConnection.DefaultUrl, MaxLength = 2048, Padding = new(6, 8, 6, 8) };
        AutomationProperties.SetName(_url, "Home Assistant address"); body.Children.Add(_url);
        body.Children.Add(new TextBlock { Text = "Long-lived access token", Margin = new(0, 14, 0, 5) });
        AutomationProperties.SetName(_token, "Long-lived access token"); body.Children.Add(_token);
        if (client.IsConfigured)
            body.Children.Add(new TextBlock { Text = "A token is saved. Leave this empty to keep it.", FontSize = 12, Opacity = .8, TextWrapping = TextWrapping.Wrap });
        body.Children.Add(_feedback);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new(0, 18, 0, 0) };
        if (client.IsConfigured)
        {
            var forget = new Button { Content = "Disconnect", MinWidth = 104, MinHeight = 40, Margin = new(0, 0, 8, 0) };
            forget.Click += (_, _) => { _client.SetConnection(null); _saved = true; Close(); };
            buttons.Children.Add(forget);
        }
        buttons.Children.Add(new Button { Content = L.T(K.CommonCancel), MinWidth = 104, MinHeight = 40, IsCancel = true });
        _save = new Button { Content = "Connect", MinWidth = 104, MinHeight = 40, IsDefault = true, Margin = new(8, 0, 0, 0) };
        _save.SetResourceReference(BackgroundProperty, "AccentBrush"); _save.SetResourceReference(ForegroundProperty, "AccentTextBrush");
        _save.Click += async (_, _) => await SaveAsync();
        buttons.Children.Add(_save);
        body.Children.Add(buttons);
        Content = body;
        Loaded += (_, _) => { if (client.IsConfigured) _url.Focus(); else _token.Focus(); };
    }

    /// <summary>Whether the connection changed (saved or removed).</summary>
    internal bool Changed => _saved;

    private async Task SaveAsync()
    {
        var url = _url.Text.Trim().TrimEnd('/');
        var token = _token.Password.Trim();
        if (!HomeAssistantConnection.ValidUrl(url)) { _feedback.Text = "Enter an address such as " + HomeAssistantConnection.DefaultUrl + "."; _url.Focus(); return; }
        if (token.Length == 0 && _client.Current is { } previous
            && Uri.TryCreate(previous.Url, UriKind.Absolute, out var before) && Uri.TryCreate(url, UriKind.Absolute, out var after)
            && before.GetLeftPart(UriPartial.Authority) == after.GetLeftPart(UriPartial.Authority))
            token = previous.Token; // same server: keep the saved token
        if (!HomeAssistantConnection.ValidToken(token)) { _feedback.Text = "Paste the whole long-lived access token."; _token.Focus(); return; }
        _save.IsEnabled = false; _feedback.Text = "Checking…";
        var connection = new HomeAssistantConnection(url, token);
        var problem = await _client.TestAsync(connection);
        if (problem != null && problem.Contains("rejected", StringComparison.Ordinal)) { _feedback.Text = problem; _save.IsEnabled = true; return; }
        try { _client.SetConnection(connection); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException)
        { _feedback.Text = "Could not save: " + ex.Message; _save.IsEnabled = true; return; }
        _saved = true;
        if (problem == null) { Close(); return; }
        // Away from home: keep it; the tile connects once Home Assistant is reachable.
        _feedback.Text = "Saved. " + problem + " The tile connects automatically when you are home.";
        _save.Content = L.T(K.CommonClose);
        _save.IsEnabled = true; _save.Click += (_, _) => Close();
    }
}
