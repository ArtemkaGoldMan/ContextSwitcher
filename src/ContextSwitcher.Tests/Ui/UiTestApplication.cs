using Avalonia;
using Avalonia.Headless;

namespace ContextSwitcher.Tests.Ui;

/// <summary>
/// Hosts the real <see cref="ContextSwitcher.App.App"/> so interaction tests run against the app's
/// actual styles, templates and resources. The headless lifetime is not
/// <c>IClassicDesktopStyleApplicationLifetime</c>, so <c>OnFrameworkInitializationCompleted</c>
/// skips the tray icon and onboarding window and only the styles are loaded.
/// </summary>
public static class UiTestApplication
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<ContextSwitcher.App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
