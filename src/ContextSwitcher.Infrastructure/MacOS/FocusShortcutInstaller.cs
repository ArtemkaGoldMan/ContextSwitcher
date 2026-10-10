using System.ComponentModel;
using System.Text;
using System.Xml;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Configuration;
using ContextSwitcher.Core.ProcessExecution;

namespace ContextSwitcher.Infrastructure.MacOS;

/// <summary>
/// Builds a Focus Shortcut as a property list, signs it with the system's own <c>shortcuts sign</c>,
/// and opens it, which makes the Shortcuts app ask the user to add it. The file is named after the
/// Shortcut, since Shortcuts names what it imports after the file.
///
/// The action is Shortcuts' "Set Focus" (<c>is.workflow.actions.dnd.set</c>): <c>Enabled</c> 1 with
/// <c>AssertionType</c> "Turned Off" turns a mode on until something turns it off; <c>Enabled</c> 0
/// turns it off. Each Shortcut names exactly one mode. One Off Shortcut for every built-in mode was
/// tried first and failed in real use: naming a mode the Mac has never had set up ("Reading") is an
/// error that stops the whole Shortcut.
/// </summary>
public sealed class FocusShortcutInstaller : IFocusShortcutInstaller
{
    /// <summary>Signing measured at about four seconds; the margin is for a slow first run.</summary>
    private static readonly TimeSpan SignTimeout = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan OpenTimeout = TimeSpan.FromSeconds(10);

    private readonly IProcessRunner processRunner;
    private readonly string workDirectory;

    /// <param name="workDirectory">Where the files are written; a fresh temporary folder by default.</param>
    public FocusShortcutInstaller(IProcessRunner processRunner, string? workDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(processRunner);

        this.processRunner = processRunner;
        this.workDirectory = workDirectory ?? Path.Combine(Path.GetTempPath(), "ContextSwitcher-Shortcuts");
    }

    /// <inheritdoc />
    public async Task<string?> OfferAsync(string modeName, bool turnOff, CancellationToken cancellationToken)
    {
        if (FocusModes.Find(modeName) is not { } mode)
        {
            return $"“{modeName}” is a Focus you made yourself, so it has to be picked in the Shortcut by hand.";
        }

        // Named exactly as the profile spells the mode, since that is the name a switch will run.
        string shortcutName = turnOff ? FocusModes.OffShortcutName(modeName) : FocusModes.ShortcutName(modeName);

        // A folder per offer: the Shortcuts app reads the file after `open` returns, so it cannot be
        // deleted straight away, and reusing one name could hand it a half-written file.
        string folder = Path.Combine(this.workDirectory, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        // `shortcuts sign` goes by the extension: the same property list named .plist is refused as
        // "not in the correct format".
        string unsigned = Path.Combine(folder, "unsigned.shortcut");
        string signed = Path.Combine(folder, shortcutName + ".shortcut");

        await File.WriteAllTextAsync(unsigned, BuildPropertyList([(mode, !turnOff)]), cancellationToken).ConfigureAwait(false);

        ProcessResult? sign = await this.TryRunAsync(
            "shortcuts", ["sign", "--mode", "anyone", "--input", unsigned, "--output", signed], SignTimeout, cancellationToken)
            .ConfigureAwait(false);
        if (sign is not { ExitCode: 0, TimedOut: false } || !File.Exists(signed))
        {
            return "Couldn't prepare the Shortcut" + (string.IsNullOrWhiteSpace(sign?.StandardError) ? "." : $": {sign.StandardError.Trim()}");
        }

        ProcessResult? open = await this.TryRunAsync("open", [signed], OpenTimeout, cancellationToken).ConfigureAwait(false);
        return open is { ExitCode: 0 } ? null : "Couldn't open the Shortcuts app.";
    }

    /// <summary>The Shortcut as an XML property list: a "Set Focus" action per mode.</summary>
    public static string BuildPropertyList(IEnumerable<(FocusMode Mode, bool On)> actions)
    {
        // UTF-8 throughout: a StringWriter would declare UTF-16 in the header of a file saved as UTF-8.
        using MemoryStream bytes = new();
        using (XmlWriter xml = XmlWriter.Create(bytes, new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false) }))
        {
            xml.WriteStartDocument();
            xml.WriteDocType("plist", "-//Apple//DTD PLIST 1.0//EN", "http://www.apple.com/DTDs/PropertyList-1.0.dtd", null);
            xml.WriteStartElement("plist");
            xml.WriteAttributeString("version", "1.0");
            xml.WriteStartElement("dict");

            Key(xml, "WFWorkflowActions");
            xml.WriteStartElement("array");
            foreach ((FocusMode mode, bool on) in actions)
            {
                xml.WriteStartElement("dict");
                Key(xml, "WFWorkflowActionIdentifier");
                xml.WriteElementString("string", "is.workflow.actions.dnd.set");
                Key(xml, "WFWorkflowActionParameters");
                xml.WriteStartElement("dict");
                Key(xml, "Enabled");
                xml.WriteElementString("integer", on ? "1" : "0");
                if (on)
                {
                    Key(xml, "AssertionType");
                    xml.WriteElementString("string", "Turned Off");
                }

                Key(xml, "FocusModes");
                xml.WriteStartElement("dict");
                Key(xml, "DisplayString");
                xml.WriteElementString("string", mode.Name);
                Key(xml, "Identifier");
                xml.WriteElementString("string", mode.Identifier);
                xml.WriteEndElement();
                xml.WriteEndElement();
                xml.WriteEndElement();
            }

            xml.WriteEndElement();

            Key(xml, "WFWorkflowClientVersion");
            xml.WriteElementString("string", "2607.0.2");
            Key(xml, "WFWorkflowMinimumClientVersion");
            xml.WriteElementString("integer", "900");
            Key(xml, "WFWorkflowMinimumClientVersionString");
            xml.WriteElementString("string", "900");
            Key(xml, "WFWorkflowIcon");
            xml.WriteStartElement("dict");
            Key(xml, "WFWorkflowIconStartColor");
            xml.WriteElementString("integer", "4282601983");
            Key(xml, "WFWorkflowIconGlyphNumber");
            xml.WriteElementString("integer", "59511");
            xml.WriteEndElement();
            foreach (string emptyList in new[] { "WFWorkflowImportQuestions", "WFWorkflowInputContentItemClasses", "WFWorkflowOutputContentItemClasses", "WFWorkflowTypes", "WFQuickActionSurfaces" })
            {
                Key(xml, emptyList);
                xml.WriteStartElement("array");
                xml.WriteEndElement();
            }

            Key(xml, "WFWorkflowHasShortcutInputVariables");
            xml.WriteStartElement("false");
            xml.WriteEndElement();

            xml.WriteEndElement();
            xml.WriteEndElement();
            xml.WriteEndDocument();
        }

        return Encoding.UTF8.GetString(bytes.ToArray());
    }

    private static void Key(XmlWriter xml, string name) => xml.WriteElementString("key", name);

    private async Task<ProcessResult?> TryRunAsync(string fileName, IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken cancellationToken)
    {
        try
        {
            return await this.processRunner
                .RunAsync(new ProcessStartOptions(fileName, arguments, timeout), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Win32Exception)
        {
            return null;
        }
    }
}
