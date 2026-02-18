using System.Windows.Input;

namespace InputAutomator.Models;

/// <summary>
/// Persisted automation configuration.
/// </summary>
public sealed class AutomationConfig
{
    // Toggle hotkey
    public ModifierKeys ToggleModifiers { get; set; } = ModifierKeys.Control | ModifierKeys.Shift;
    public Key ToggleKey { get; set; } = Key.T;

    // Bindings
    public List<InputBinding> HoldBindings { get; set; } = [];
    public List<InputBinding> RepeatBindings { get; set; } = [];

    // Repeat interval in seconds
    public double RepeatIntervalSeconds { get; set; } = 1.0;

    // Humanization
    public bool HumanizeInterval { get; set; } = false;
    public double HumanizeJitterSeconds { get; set; } = 0.05;

    public string ToggleHotkeyDisplay =>
        (ToggleModifiers != ModifierKeys.None ? $"{ToggleModifiers}+" : "") + ToggleKey;
}
