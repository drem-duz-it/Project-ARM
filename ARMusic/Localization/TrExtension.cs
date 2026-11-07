namespace ARMusic;

// Per i testi dentro i modelli (DataTemplate) del XAML, creati a runtime:  Text="{loc:Tr Non scaricato}"
// con xmlns:loc="clr-namespace:ARMusic"
[ContentProperty(nameof(Text))]
public class TrExtension : IMarkupExtension<string>
{
    public string Text { get; set; }

    public string ProvideValue(IServiceProvider serviceProvider) => Loc.T(Text);

    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider) => ProvideValue(serviceProvider);
}
