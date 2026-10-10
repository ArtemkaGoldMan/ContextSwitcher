using Avalonia;
using Avalonia.Controls;

namespace ContextSwitcher.App.Controls;

/// <summary>
/// The one layout for a labelled setting, on every page: the label - with an optional line of
/// explanation under it - on the left, the control on the right. Its look lives in
/// Styles/Controls.axaml, which also gives every dropdown and text box placed directly in a row
/// the same width, so their right edges and the toggles' line up down the whole page.
///
/// This exists because the same kind of row had been built three ways - a fixed label column with
/// a stretched control, a label with a narrow control pushed right, and no label at all - and
/// each page looked like a different app.
/// </summary>
public class FormRow : ContentControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<FormRow, string?>(nameof(Title));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<FormRow, string?>(nameof(Description));

    /// <summary>The label, such as "Focus mode".</summary>
    public string? Title
    {
        get => this.GetValue(TitleProperty);
        set => this.SetValue(TitleProperty, value);
    }

    /// <summary>An optional line under the label saying what the setting does; hidden when empty.</summary>
    public string? Description
    {
        get => this.GetValue(DescriptionProperty);
        set => this.SetValue(DescriptionProperty, value);
    }
}
