using System.Windows.Markup;

namespace DiskPulse.App.Resources;

/// <summary>XAML access to UI strings: <c>Text="{res:Loc Settings_Title}"</c>. Read once, when the window loads.</summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class LocExtension(string key) : MarkupExtension
{
    [ConstructorArgument("key")]
    public string Key { get; set; } = key;

    public override object ProvideValue(IServiceProvider serviceProvider) => Strings.Get(Key);
}
