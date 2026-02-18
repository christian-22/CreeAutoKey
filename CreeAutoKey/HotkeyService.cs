using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using static InputAutomator.Services.NativeMethods;

namespace InputAutomator.Services;

/// <summary>
/// Manages global hotkeys via Win32 RegisterHotKey.
/// Emergency stop hotkeys (Esc, Ctrl+Alt+End) are registered only while automation is active.
/// </summary>
public sealed class HotkeyService : IDisposable
{
    // Hotkey IDs
    private const int ID_TOGGLE = 1;
    private const int ID_ESC = 2;
    private const int ID_CTRL_ALT_END = 3;

    private IntPtr _hwnd;
    private HwndSource? _source;
    private bool _toggleRegistered;
    private bool _emergencyRegistered;

    public event Action? TogglePressed;
    public event Action<string>? EmergencyStopPressed;

    private readonly RingBufferLogger _logger;

    public HotkeyService(RingBufferLogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Initialize with a WPF window to hook into its message loop.
    /// Call after the window's SourceInitialized event.
    /// </summary>
    public void Initialize(Window window)
    {
        var helper = new WindowInteropHelper(window);
        _hwnd = helper.Handle;
        _source = HwndSource.FromHwnd(_hwnd);
        _source?.AddHook(WndProc);
    }

    /// <summary>Register the user-configurable toggle hotkey.</summary>
    public bool RegisterToggle(ModifierKeys modifiers, Key key)
    {
        UnregisterToggle();

        uint mods = ToWin32Modifiers(modifiers) | MOD_NOREPEAT;
        uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);

        if (RegisterHotKey(_hwnd, ID_TOGGLE, mods, vk))
        {
            _toggleRegistered = true;
            _logger.Log($"Toggle hotkey registered: {modifiers}+{key}");
            return true;
        }

        _logger.Log($"FAILED to register toggle hotkey: {modifiers}+{key} (may be in use)");
        return false;
    }

    public void UnregisterToggle()
    {
        if (_toggleRegistered)
        {
            UnregisterHotKey(_hwnd, ID_TOGGLE);
            _toggleRegistered = false;
        }
    }

    /// <summary>Register Esc and Ctrl+Alt+End. Call when automation starts.</summary>
    public void RegisterEmergencyStops()
    {
        if (_emergencyRegistered) return;

        // Esc (no modifiers)
        RegisterHotKey(_hwnd, ID_ESC, MOD_NOREPEAT, 0x1B);
        // Ctrl+Alt+End
        RegisterHotKey(_hwnd, ID_CTRL_ALT_END, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, 0x23);

        _emergencyRegistered = true;
        _logger.Log("Emergency stop hotkeys registered (Esc, Ctrl+Alt+End)");
    }

    /// <summary>Unregister emergency hotkeys. Call when automation stops.</summary>
    public void UnregisterEmergencyStops()
    {
        if (!_emergencyRegistered) return;

        UnregisterHotKey(_hwnd, ID_ESC);
        UnregisterHotKey(_hwnd, ID_CTRL_ALT_END);

        _emergencyRegistered = false;
        _logger.Log("Emergency stop hotkeys unregistered");
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            switch (id)
            {
                case ID_TOGGLE:
                    _logger.Log("Hotkey: TOGGLE");
                    TogglePressed?.Invoke();
                    handled = true;
                    break;
                case ID_ESC:
                    _logger.Log("Hotkey: ESC (emergency stop)");
                    EmergencyStopPressed?.Invoke("Esc");
                    handled = true;
                    break;
                case ID_CTRL_ALT_END:
                    _logger.Log("Hotkey: Ctrl+Alt+End (emergency stop)");
                    EmergencyStopPressed?.Invoke("Ctrl+Alt+End");
                    handled = true;
                    break;
            }
        }
        return IntPtr.Zero;
    }

    private static uint ToWin32Modifiers(ModifierKeys mods)
    {
        uint result = 0;
        if (mods.HasFlag(ModifierKeys.Alt)) result |= MOD_ALT;
        if (mods.HasFlag(ModifierKeys.Control)) result |= MOD_CONTROL;
        if (mods.HasFlag(ModifierKeys.Shift)) result |= MOD_SHIFT;
        if (mods.HasFlag(ModifierKeys.Windows)) result |= MOD_WIN;
        return result;
    }

    public void Dispose()
    {
        UnregisterToggle();
        UnregisterEmergencyStops();
        _source?.RemoveHook(WndProc);
    }
}
