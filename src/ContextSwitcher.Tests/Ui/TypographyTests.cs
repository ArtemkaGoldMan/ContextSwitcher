using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ContextSwitcher.App.Views;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Tests.App;

namespace ContextSwitcher.Tests.Ui;

[Collection(AppHostTestCollection.Name)]
public sealed class TypographyTests : UiTest
{
    /// <summary>
    /// Ukrainian came out spread wide - "Т е с т р о б о т а" - while English looked fine. The theme's
    /// font list let Cyrillic fall through to Hiragino Sans GB, a Chinese font whose Cyrillic letters
    /// are as wide as ideographs: nearly twice the width of the same Latin text. The app's own font
    /// keeps the two in proportion.
    /// </summary>
    [Fact]
    public async Task CyrillicIsSetAsTightlyAsLatin()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        await OnUiThreadAsync(() =>
        {
            Assert.True(Application.Current!.TryFindResource("DefaultFontFamily", out object? resource));
            FontFamily family = Assert.IsType<FontFamily>(resource);
            Assert.Equal(".AppleSystemUIFont", family.FamilyNames[0]);

            double Width(string text) => new FormattedText(
                text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(family), 13, Brushes.Black).WidthIncludingTrailingWhitespace;

            double ratio = Width("Перевірити пошту, Жовтень, Таблиці") / Width("Pereviryty poshtu, Zhovten, Tablytsi");
            Assert.InRange(ratio, 0.8, 1.3);
        });
    }

    /// <summary>Real controls pick the font up too: the dashboard's text is in the app's font.</summary>
    [Fact]
    public async Task TheDashboardDrawsInTheAppsFont()
    {
        await OnUiThreadAsync(() =>
        {
            UiScenario scenario = UiScenario.With(new AppConfiguration
            {
                ActiveContextId = "work",
                Contexts =
                [
                    new ContextDefinition { Id = "work", DisplayName = "Тест робота", AccentColor = "#2F6FED", Notes = ["Стендап о 10:00"] },
                    new ContextDefinition { Id = "home", DisplayName = "Особисте", AccentColor = "#20A67A" }
                ]
            });
            DashboardWindow window = new(scenario.Dashboard(), () => { });
            window.Show();
            Settle(window);

            List<TextBlock> cyrillic = FindAll<TextBlock>(window).Where(t => t.Text is "Тест робота" or "Особисте" or "Стендап о 10:00").ToList();
            Assert.True(cyrillic.Count >= 3);
            Assert.All(cyrillic, t => Assert.Equal(".AppleSystemUIFont", t.FontFamily.FamilyNames[0]));
            window.Close();
        });
    }
}
