using System.Windows;
using System.Windows.Markup;

namespace ReStart.App;

public sealed class LocExtension : MarkupExtension
{
    public string Key { get; set; } = "";
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        LocalizationResources.Keys.Add(Key);
        Application.Current.Resources["Ui." + Key] = L.T(Key);
        return new DynamicResourceExtension("Ui." + Key).ProvideValue(serviceProvider);
    }
}

internal static class LocalizationResources
{
    public static HashSet<string> Keys { get; } = [];
    public static void Apply(string language)
    {
        L.SetLanguage(language);
        foreach (var key in Keys) Application.Current.Resources["Ui." + key] = L.T(key);
        System.Globalization.CultureInfo.CurrentCulture = L.Culture;
        System.Globalization.CultureInfo.CurrentUICulture = L.Culture;
    }
}
