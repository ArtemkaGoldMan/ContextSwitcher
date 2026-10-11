using System.Globalization;
using System.Reflection;
using System.Xml.Linq;
using ContextSwitcher.App.Resources;
using ContextSwitcher.Core.Configuration.Validation;
using ContextSwitcher.Core.Contexts;
using ContextSwitcher.Core.Messages;

namespace ContextSwitcher.Tests.Localization;

/// <summary>Messages from the switch engine, the configuration check and the updater, shown in the app's language.</summary>
public sealed class MessageTextTests
{
    private static readonly Type[] Catalogs = [typeof(SwitchMessages), typeof(ConfigurationMessages), typeof(UpdateMessages), typeof(FocusMessages)];

    /// <summary>
    /// Every message the engine can produce is in Strings.resx with the same English, and nothing there
    /// is left over from a message that's gone - so translators translate exactly what the app says.
    /// </summary>
    [Fact]
    public void EveryMessageIsInTheResourcesWithTheSameEnglish()
    {
        Dictionary<string, string> templates = Catalogs
            .SelectMany(catalog => catalog.GetFields(BindingFlags.Public | BindingFlags.Static))
            .Select(field => (MessageTemplate)field.GetValue(null)!)
            .ToDictionary(template => $"Msg_{template.Key}", template => template.English);
        Dictionary<string, string> resources = XDocument.Load(StringsResx()).Root!.Elements("data")
            .Where(data => ((string)data.Attribute("name")!).StartsWith("Msg_", StringComparison.Ordinal))
            .ToDictionary(data => (string)data.Attribute("name")!, data => (string)data.Element("value")!);

        Assert.Equal(templates.Keys.Order(), resources.Keys.Order());
        Assert.All(templates, template => Assert.Equal(template.Value, resources[template.Key]));
    }

    [Fact]
    public void AMessageIsShownInTheAppsLanguage()
    {
        UserMessage message = SwitchMessages.CouldNotClose.With("Slack, Zoom");

        Assert.Equal("Could not close: Slack, Zoom.", MessageText.Of(message));
        Assert.Equal("Не вдалося закрити: Slack, Zoom.", InUkrainian(() => MessageText.Of(message)));
    }

    /// <summary>A switch recorded before messages had keys still shows - in English, as it was written.</summary>
    [Fact]
    public void AnOlderRecordWithoutAKeyShowsItsEnglish()
    {
        StateError older = new() { StepId = "close", Message = "Could not close: Slack." };

        Assert.Equal("Could not close: Slack.", InUkrainian(() => MessageText.Of(older)));
    }

    [Fact]
    public void ARecordWithAKeyIsTranslated()
    {
        StateError error = new()
        {
            StepId = "focus",
            Message = "Step timed out after 45 seconds.",
            MessageKey = SwitchMessages.StepTimedOut.Key,
            MessageArguments = ["45"]
        };

        Assert.Equal("Крок не завершився за 45 с.", InUkrainian(() => MessageText.Of(error)));
    }

    /// <summary>A key the app doesn't know - from a newer build, say - falls back to the English it carries.</summary>
    [Fact]
    public void AnUnknownKeyFallsBackToTheEnglish()
    {
        UserMessage unknown = new("Switch_SomethingNew", ["x"], "Something new happened.");

        Assert.Equal("Something new happened.", MessageText.Of(unknown));
    }

    [Fact]
    public void AConfigurationErrorNamesTheSettingThenTheProblem()
    {
        ConfigurationValidationError error = ConfigurationValidationError.Of("contexts[0].accentColor", ConfigurationMessages.AccentColorInvalid.With());

        Assert.Equal("contexts[0].accentColor: Accent color must be a 6-digit hex color.", MessageText.Of(error));
        Assert.Equal("contexts[0].accentColor: Колір має бути шестизначним hex-кодом.", InUkrainian(() => MessageText.Of(error)));
    }

    private static string InUkrainian(Func<string> show)
    {
        CultureInfo previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("uk-UA");
        try
        {
            return show();
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    private static string StringsResx()
    {
        DirectoryInfo? folder = new(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "ContextSwitcher.sln")))
        {
            folder = folder.Parent;
        }

        return Path.Combine(folder!.FullName, "src", "ContextSwitcher.App", "Resources", "Strings.resx");
    }
}
