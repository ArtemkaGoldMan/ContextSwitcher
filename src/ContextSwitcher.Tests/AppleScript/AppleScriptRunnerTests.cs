using ContextSwitcher.Core.ProcessExecution;
using ContextSwitcher.Infrastructure.AppleScript;
using ContextSwitcher.Infrastructure.ProcessExecution;

namespace ContextSwitcher.Tests.AppleScript;

public sealed class AppleScriptRunnerTests
{
    [Fact]
    public async Task RunAsyncExecutesScriptThroughOsascript()
    {
        AppleScriptRunner runner = new(new ProcessRunner());

        ProcessResult result = await runner.RunAsync("return 2 + 2", TimeSpan.FromSeconds(10), CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("4", result.StandardOutput.Trim());
    }
}
