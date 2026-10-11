using System.Globalization;
using System.Runtime.InteropServices;

namespace ContextSwitcher.Infrastructure.MacOS;

/// <summary>
/// The languages the person has listed in System Settings → Language &amp; Region, most preferred first
/// - "en-US", "pl-PL", "uk-PL" - including a language chosen for this app alone, which macOS keeps in
/// the app's own preferences.
///
/// .NET's own culture only reflects the first of them, so a Polish-first Mac that also lists
/// Ukrainian would get English rather than the Ukrainian it would prefer to Polish-less English.
/// </summary>
public static class PreferredLanguages
{
    private const string ObjectiveC = "/usr/lib/libobjc.A.dylib";

    /// <summary>The list from macOS, or the current culture alone where macOS can't be asked.</summary>
    public static IReadOnlyList<string> Get()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return [CultureInfo.CurrentUICulture.Name];
        }

        try
        {
            IntPtr pool = objc_autoreleasePoolPush();
            try
            {
                IntPtr locale = objc_getClass("NSLocale");
                IntPtr languages = SendReturningPointer(locale, sel_registerName("preferredLanguages"));
                nuint count = languages == IntPtr.Zero ? 0 : SendReturningNUInt(languages, sel_registerName("count"));
                List<string> result = [];
                for (nuint i = 0; i < count; i++)
                {
                    IntPtr language = SendReturningPointerWithIndex(languages, sel_registerName("objectAtIndex:"), i);
                    if (Marshal.PtrToStringUTF8(SendReturningPointer(language, sel_registerName("UTF8String"))) is { Length: > 0 } name)
                    {
                        result.Add(name);
                    }
                }

                return result.Count > 0 ? result : [CultureInfo.CurrentUICulture.Name];
            }
            finally
            {
                objc_autoreleasePoolPop(pool);
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return [CultureInfo.CurrentUICulture.Name];
        }
    }

    [DllImport(ObjectiveC)]
    private static extern IntPtr objc_getClass([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(ObjectiveC)]
    private static extern IntPtr sel_registerName([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(ObjectiveC)]
    private static extern IntPtr objc_autoreleasePoolPush();

    [DllImport(ObjectiveC)]
    private static extern void objc_autoreleasePoolPop(IntPtr pool);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr SendReturningPointer(IntPtr receiver, IntPtr selector);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern IntPtr SendReturningPointerWithIndex(IntPtr receiver, IntPtr selector, nuint index);

    [DllImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static extern nuint SendReturningNUInt(IntPtr receiver, IntPtr selector);
}
