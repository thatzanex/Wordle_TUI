namespace WordleApp.Models;

// === SETTINGS ROW ===

/// <summary>
/// One line of the settings screen, prepared for the renderer. The renderer only
/// draws what this carries; deciding what a row contains is the editor's job.
/// </summary>
/// <param name="Label">Already translated caption on the left.</param>
/// <param name="Value">Already translated value on the right, empty for actions.</param>
/// <param name="SwatchColor">Colour to preview as a filled block, or null for none.</param>
public readonly record struct SettingsRow(string Label, string Value, string? SwatchColor);
