using System.Text;
using ContextSwitcher.App.Services;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.Logging;
using ContextSwitcher.Infrastructure.Logging;

namespace ContextSwitcher.Tests.Properties;

/// <summary>
/// The promise a log makes - nothing private in it - checked on random text rather than the few
/// examples someone thought of: a secret put into an address, an email or a path is never in what is kept.
/// </summary>
public sealed class RedactionInvariantsTests
{
    public static TheoryData<int> Seeds => new(Enumerable.Range(1, 150));

    [Theory]
    [MemberData(nameof(Seeds))]
    public void ASecretInAnAddressEmailOrPathIsNeverKept(int seed)
    {
        (string text, string[] secrets) = Compose(seed);

        string kept = LogRedactor.Redact(text);

        foreach (string secret in secrets)
        {
            Assert.DoesNotContain(secret, kept, StringComparison.Ordinal);
        }
    }

    /// <summary>Cleaning what is already clean changes nothing: an old line cleaned again, as in a report, is the same.</summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void CleaningTwiceIsCleaningOnce(int seed)
    {
        (string text, _) = Compose(seed);

        string once = LogRedactor.Redact(text);

        Assert.Equal(once, LogRedactor.Redact(once));
    }

    /// <summary>Text with nothing private in it is kept as it is.</summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void PlainWordsAreKeptAsTheyAre(int seed)
    {
        Random random = new(seed);
        string plain = string.Join(' ', Enumerable.Range(0, 12).Select(_ => Words[random.Next(Words.Length)]));

        Assert.Equal(plain, LogRedactor.Redact(plain));
    }

    /// <summary>No profile name, app or address a person has chosen is in a diagnostic report, whatever it is.</summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void AReportNeverHoldsWhatThePersonNamed(int seed)
    {
        Random random = new(seed);
        string profile = $"Profile-{Token(random)}";
        string app = $"App-{Token(random)}";
        string site = $"https://{Token(random)}.example/{Token(random)}";
        AppConfiguration configuration = new()
        {
            ActiveContextId = "a",
            IgnoredApps = [app],
            Contexts =
            [
                new ContextDefinition
                {
                    Id = "a",
                    DisplayName = profile,
                    LaunchApps = [app],
                    BrowserManagement = new BrowserManagementConfig { Mode = BrowserManagementMode.Urls, Urls = [site] }
                }
            ]
        };

        string report = DiagnosticsReport.Build(new DiagnosticsInput(
            new RunInfo("ContextSwitcher", "0.6.1", "macOS", "26.6", "arm64", "release", 1),
            configuration,
            true,
            0,
            [],
            [],
            DateTimeOffset.UnixEpoch));

        Assert.DoesNotContain(profile, report, StringComparison.Ordinal);
        Assert.DoesNotContain(app, report, StringComparison.Ordinal);
        Assert.DoesNotContain(site, report, StringComparison.Ordinal);
    }

    private static readonly string[] Words = ["switch", "step", "finished", "with", "status", "Warning", "browser", "Focus", "closed", "apps", "of", "3", "tabs"];

    private static (string Text, string[] Secrets) Compose(int seed)
    {
        Random random = new(seed);
        List<string> secrets = [];
        StringBuilder text = new();

        for (int i = 0; i < 6; i++)
        {
            string secret = Token(random);
            secrets.Add(secret);
            text.Append(random.Next(5) switch
            {
                0 => $"opened https://{Token(random)}.example/{secret}/page?token={Token(random)}#{secret}2 ",
                1 => $"for {secret}@mail.example ",
                2 => $"read /Users/{secret}/Library/x.json ",
                3 => $"went to http://user:{secret}@host.example/inbox ",
                _ => $"{Words[random.Next(Words.Length)]} "
            });
        }

        // A secret that was only given words around it is not a leak when it is not private.
        return (text.ToString(), [.. secrets.Where(secret => text.ToString().Contains(secret, StringComparison.Ordinal)
            && (text.ToString().Contains($"/{secret}/page", StringComparison.Ordinal)
                || text.ToString().Contains($"{secret}@", StringComparison.Ordinal)
                || text.ToString().Contains($"/Users/{secret}", StringComparison.Ordinal)
                || text.ToString().Contains($":{secret}@", StringComparison.Ordinal)))]);
    }

    private static string Token(Random random) =>
        string.Concat(Enumerable.Range(0, 10).Select(_ => "abcdefghjkmnpqrstuvwxyz23456789"[random.Next(31)]));
}
