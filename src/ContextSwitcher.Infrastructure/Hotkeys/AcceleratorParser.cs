using SharpHook.Data;

namespace ContextSwitcher.Infrastructure.Hotkeys;

/// <summary>
/// Parses accelerator strings like <c>"Cmd+Alt+Ctrl+W"</c> (agent.md section 6.1's
/// <c>hotkeys[].accelerator</c> format) into a SharpHook modifier mask and key code.
/// </summary>
public static class AcceleratorParser
{
    /// <summary>
    /// Attempts to parse an accelerator string into a modifier <see cref="EventMask"/> and a
    /// single trailing <see cref="KeyCode"/>. Requires at least one modifier and exactly one key.
    /// </summary>
    public static bool TryParse(string accelerator, out EventMask modifiers, out KeyCode key)
    {
        modifiers = EventMask.None;
        key = KeyCode.VcUndefined;

        if (string.IsNullOrWhiteSpace(accelerator))
        {
            return false;
        }

        string[] parts = accelerator.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
        {
            return false;
        }

        for (int i = 0; i < parts.Length - 1; i++)
        {
            switch (parts[i].ToLowerInvariant())
            {
                case "cmd" or "command":
                    modifiers |= EventMask.Meta;
                    break;
                case "alt" or "option":
                    modifiers |= EventMask.Alt;
                    break;
                case "ctrl" or "control":
                    modifiers |= EventMask.Ctrl;
                    break;
                case "shift":
                    modifiers |= EventMask.Shift;
                    break;
                default:
                    return false;
            }
        }

        string keyToken = NormalizeKeyToken(parts[^1]);
        return Enum.TryParse($"Vc{keyToken}", ignoreCase: true, out key);
    }

    private static string NormalizeKeyToken(string token) => token switch
    {
        "Esc" => "Escape",
        "Return" => "Enter",
        "Del" => "Delete",
        _ => token
    };
}
