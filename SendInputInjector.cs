using System.Runtime.InteropServices;
using InputAutomator.Models;
using static InputAutomator.Services.NativeMethods;

namespace InputAutomator.Services;

/// <summary>
/// Injects keyboard/mouse input via Win32 SendInput.
/// Tracks which inputs are currently held so they can be reliably released.
/// </summary>
public sealed class SendInputInjector
{
    private readonly object _lock = new();
    private readonly HashSet<ushort> _heldKeys = [];
    private readonly HashSet<MouseBtn> _heldMouseButtons = [];

    private static readonly int InputSize = Marshal.SizeOf<INPUT>();

    public IReadOnlySet<ushort> HeldKeys { get { lock (_lock) return [.. _heldKeys]; } }
    public IReadOnlySet<MouseBtn> HeldMouseButtons { get { lock (_lock) return [.. _heldMouseButtons]; } }

    // ── Keyboard ───────────────────────────────────────────────────

    public void KeyDown(ushort vk)
    {
        var input = MakeKeyInput(vk, keyUp: false);
        Send(input);
        lock (_lock) _heldKeys.Add(vk);
    }

    public void KeyUp(ushort vk)
    {
        var input = MakeKeyInput(vk, keyUp: true);
        Send(input);
        lock (_lock) _heldKeys.Remove(vk);
    }

    public void KeyClick(ushort vk)
    {
        INPUT[] inputs = [MakeKeyInput(vk, false), MakeKeyInput(vk, true)];
        Send(inputs);
    }

    // ── Mouse ──────────────────────────────────────────────────────

    public void MouseDown(MouseBtn btn)
    {
        var input = MakeMouseInput(MouseDownFlag(btn));
        Send(input);
        lock (_lock) _heldMouseButtons.Add(btn);
    }

    public void MouseUp(MouseBtn btn)
    {
        var input = MakeMouseInput(MouseUpFlag(btn));
        Send(input);
        lock (_lock) _heldMouseButtons.Remove(btn);
    }

    public void MouseClick(MouseBtn btn)
    {
        INPUT[] inputs = [MakeMouseInput(MouseDownFlag(btn)), MakeMouseInput(MouseUpFlag(btn))];
        Send(inputs);
    }

    // ── Release All ────────────────────────────────────────────────

    /// <summary>
    /// Releases every key and mouse button the app has held down.
    /// Safe to call multiple times.
    /// </summary>
    public void ReleaseAll()
    {
        List<INPUT> inputs = [];

        lock (_lock)
        {
            foreach (var vk in _heldKeys)
                inputs.Add(MakeKeyInput(vk, keyUp: true));
            _heldKeys.Clear();

            foreach (var btn in _heldMouseButtons)
                inputs.Add(MakeMouseInput(MouseUpFlag(btn)));
            _heldMouseButtons.Clear();
        }

        if (inputs.Count > 0)
            Send([.. inputs]);
    }

    // ── Helpers ────────────────────────────────────────────────────

    private static void Send(INPUT input) => Send([input]);

    private static void Send(INPUT[] inputs)
    {
        SendInput((uint)inputs.Length, inputs, InputSize);
    }

    private static uint MouseDownFlag(MouseBtn btn) => btn switch
    {
        MouseBtn.Left => MOUSEEVENTF_LEFTDOWN,
        MouseBtn.Right => MOUSEEVENTF_RIGHTDOWN,
        MouseBtn.Middle => MOUSEEVENTF_MIDDLEDOWN,
        _ => throw new ArgumentOutOfRangeException(nameof(btn))
    };

    private static uint MouseUpFlag(MouseBtn btn) => btn switch
    {
        MouseBtn.Left => MOUSEEVENTF_LEFTUP,
        MouseBtn.Right => MOUSEEVENTF_RIGHTUP,
        MouseBtn.Middle => MOUSEEVENTF_MIDDLEUP,
        _ => throw new ArgumentOutOfRangeException(nameof(btn))
    };
}
