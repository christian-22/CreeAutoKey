using System.Text.Json.Serialization;
using System.Windows.Input;

namespace InputAutomator.Models;

public enum InputBindingKind { KeyboardKey, MouseButton }

public enum MouseBtn { Left, Right, Middle }

/// <summary>
/// Represents a single input that can be held or repeated.
/// </summary>
public sealed class InputBinding
{
    public InputBindingKind Kind { get; set; }

    /// <summary>Virtual key code (only for KeyboardKey).</summary>
    public ushort VirtualKey { get; set; }

    /// <summary>Mouse button (only for MouseButton).</summary>
    public MouseBtn MouseButton { get; set; }

    [JsonIgnore]
    public string DisplayName => Kind switch
    {
        InputBindingKind.KeyboardKey => KeyInterop.KeyFromVirtualKey(VirtualKey).ToString(),
        InputBindingKind.MouseButton => $"Mouse {MouseButton}",
        _ => "?"
    };

    public static InputBinding FromKey(Key key)
    {
        return new InputBinding
        {
            Kind = InputBindingKind.KeyboardKey,
            VirtualKey = (ushort)KeyInterop.VirtualKeyFromKey(key)
        };
    }

    public static InputBinding FromMouse(MouseBtn btn)
    {
        return new InputBinding
        {
            Kind = InputBindingKind.MouseButton,
            MouseButton = btn
        };
    }

    public override string ToString() => DisplayName;

    public override bool Equals(object? obj) =>
        obj is InputBinding other &&
        Kind == other.Kind &&
        VirtualKey == other.VirtualKey &&
        MouseButton == other.MouseButton;

    public override int GetHashCode() => HashCode.Combine(Kind, VirtualKey, MouseButton);
}
