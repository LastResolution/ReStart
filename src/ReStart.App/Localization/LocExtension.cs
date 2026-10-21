using System.Windows;
using System.Windows.Markup;

namespace ReStart.App;

/// <summary>Provides a dynamic WPF resource for a typed, language-independent text identifier.</summary>
public sealed class LocExtension : MarkupExtension
{
    /// <summary>Gets or sets the compile-time text identifier to bind as a dynamic resource.</summary>
    public ReStart.Core.TextKey Key { get; set; }
    /// <summary>Registers the key for language refresh and returns a dynamic-resource expression.</summary>
    /// <param name="serviceProvider">XAML service provider for the target dependency property.</param>
    /// <returns>A dynamic resource expression that updates with the selected language.</returns>
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        LocalizationResources.Keys.Add(Key);
        Application.Current.Resources["Ui." + Key] = L.T(Key);
        return new DynamicResourceExtension("Ui." + Key).ProvideValue(serviceProvider);
    }
}

internal static class LocalizationResources
{
    public static HashSet<ReStart.Core.TextKey> Keys { get; } = [];
    public static void Apply(string language)
    {
        L.SetLanguage(language);
        foreach (var key in Keys) Application.Current.Resources["Ui." + key] = L.T(key);
        System.Globalization.CultureInfo.CurrentCulture = L.Culture;
        System.Globalization.CultureInfo.CurrentUICulture = L.Culture;
    }
}
