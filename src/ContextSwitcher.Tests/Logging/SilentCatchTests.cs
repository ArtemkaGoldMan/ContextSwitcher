using System.Text.RegularExpressions;

namespace ContextSwitcher.Tests.Logging;

/// <summary>
/// An exception swallowed without a trace is a problem nobody can find later. Every catch that does
/// nothing - or only returns - has to say which of two things it is: logged, or deliberately not,
/// with the reason in a comment that starts <c>Not logged:</c>. This fails when a new one says neither.
/// </summary>
public sealed partial class SilentCatchTests
{
    private static readonly string[] Quiet =
    [
        "return;", "return null;", "return false;", "return true;", "continue;", "break;"
    ];

    [Fact]
    public void EveryCatchThatSwallowsAnExceptionIsLoggedOrSaysWhyNot()
    {
        List<string> unexplained = [];

        foreach (string file in SourceFiles())
        {
            string[] lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                if (!CatchClause().IsMatch(lines[i]))
                {
                    continue;
                }

                List<string> body = BodyOf(lines, i);
                List<string> code = body
                    .Select(line => line.Trim())
                    .Where(line => line.Length > 0 && line != "{" && line != "}" && !line.StartsWith("//", StringComparison.Ordinal) && !line.StartsWith('#'))
                    .ToList();
                bool swallows = code.Count == 0 || (code.Count == 1 && Quiet.Contains(code[0]));
                string text = string.Join('\n', body);
                bool explained = text.Contains("Not logged:", StringComparison.Ordinal)
                    || text.Contains("AppLog.", StringComparison.Ordinal)
                    || text.Contains("logger", StringComparison.OrdinalIgnoreCase)
                    || text.Contains("Log(", StringComparison.Ordinal);

                if (swallows && !explained)
                {
                    unexplained.Add($"{Path.GetRelativePath(RepositoryRoot(), file)}:{i + 1}  {lines[i].Trim()}");
                }
            }
        }

        Assert.True(unexplained.Count == 0, "catch blocks that swallow an exception with no log and no 'Not logged:' reason:\n" + string.Join('\n', unexplained));
    }

    private static List<string> BodyOf(string[] lines, int catchLine)
    {
        int open = catchLine + 1;
        while (open < lines.Length && !lines[open].Trim().StartsWith('{'))
        {
            open++;
        }

        List<string> body = [];
        int depth = 0;
        for (int i = open; i < lines.Length; i++)
        {
            depth += lines[i].Count(c => c == '{') - lines[i].Count(c => c == '}');
            body.Add(lines[i]);
            if (depth <= 0)
            {
                break;
            }
        }

        return body;
    }

    private static IEnumerable<string> SourceFiles()
    {
        string root = RepositoryRoot();
        return new[] { "ContextSwitcher.Core", "ContextSwitcher.Infrastructure", "ContextSwitcher.App" }
            .SelectMany(project => Directory.EnumerateFiles(Path.Combine(root, "src", project), "*.cs", SearchOption.AllDirectories))
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ContextSwitcher.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The repository root, with ContextSwitcher.sln, was not found above the test run.");
    }

    [GeneratedRegex(@"^\s*catch\b")]
    private static partial Regex CatchClause();
}
