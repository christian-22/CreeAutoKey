# Input Automator

A minimal Windows desktop app (.NET 10, C# 14, WPF) that automates keyboard/mouse input via Win32 `SendInput`.

## Features

- **Hold** selected keys/mouse buttons down indefinitely
- **Repeat-click** selected keys/mouse buttons at a configurable interval (supports decimals, e.g. 0.1s)
- **Global toggle hotkey** (configurable, e.g. `Ctrl+Shift+T`) works even when unfocused
- **Hard emergency stops**: `Esc` and `Ctrl+Alt+End` (non-configurable, always active during automation)
- **3-2-1 countdown** before activation (cancelable)
- **Developer Mode** window with live state, config snapshot, and event log
- **Humanization** option: random jitter on repeat intervals
- Config auto-saved to `%APPDATA%/InputAutomator/config.json`

## Building & Running

### Prerequisites
- Windows 10/11
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### Build
```bash
cd InputAutomator
dotnet build
```

### Run
```bash
dotnet run
```

### Publish (single-file)
```bash
dotnet publish -c Release -r win-x64 --self-contained
```

## Architecture

```
InputAutomator/
├── Models/
│   ├── InputBinding.cs        # Key/mouse binding data model
│   ├── AutomationConfig.cs    # Persisted configuration
│   └── AutomationState.cs     # State machine enum
├── Services/
│   ├── NativeMethods.cs       # Win32 P/Invoke (SendInput, RegisterHotKey)
│   ├── SendInputInjector.cs   # Input injection + tracking held inputs
│   ├── HotkeyService.cs       # Global hotkey registration via WM_HOTKEY
│   ├── AutomationEngine.cs    # State machine: Idle→Countdown→Running→Idle
│   ├── ConfigStore.cs         # JSON persistence to AppData
│   └── RingBufferLogger.cs    # Observable ring buffer for dev UI
├── Views/
│   ├── MainWindow.xaml/.cs    # Configuration UI
│   ├── MiniStatusWindow.xaml/.cs  # Always-on-top tiny status overlay
│   └── DeveloperModeWindow.xaml/.cs  # Debug panel + log stream
├── App.xaml/.cs               # Entry point + global exception safety
└── InputAutomator.csproj
```

### Key Design Decisions

1. **RegisterHotKey for toggle**: The toggle hotkey uses Win32 `RegisterHotKey` so it works globally even when the app is unfocused.

2. **Emergency stops registered only when active**: `Esc` and `Ctrl+Alt+End` are registered as global hotkeys only while automation is in Countdown or Running state, so they don't interfere with normal Esc usage.

3. **SendInput for injection**: All input is injected via `SendInput` P/Invoke with proper scan codes and extended-key flags.

4. **Deterministic release**: The `SendInputInjector` tracks every key/button it has pressed down, so `ReleaseAll()` deterministically releases exactly what was held.

5. **Multiple stop paths**: Inputs are released on toggle-off, emergency stop, window close, unhandled exceptions, process exit, and session ending events.

## Safety Notes

### Why Ctrl+Alt+Delete Can't Be Captured
`Ctrl+Alt+Delete` is intercepted by the Windows kernel (specifically, the Winlogon process) before it reaches any user-mode application. This is by design — it's the Windows Secure Attention Sequence (SAS). No `RegisterHotKey`, keyboard hook, or any user-mode code can intercept it. This makes it the ultimate system bailout: even if this app or any other app is misbehaving, `Ctrl+Alt+Delete` will always bring up the Windows security screen where you can open Task Manager.

The app displays `Ctrl+Alt+Delete` as "system bailout" in the status text but does not attempt to capture it.

### Emergency Stop Behavior
- **Esc**: Immediately cancels automation and releases all held inputs
- **Ctrl+Alt+End**: Same behavior, works even if Esc somehow fails
- Both are registered as global hotkeys only while automation is active
- The app always releases all held inputs when stopping, regardless of the stop reason

### Best-Effort Crash Safety
The app hooks into `DispatcherUnhandledException`, `TaskScheduler.UnobservedTaskException`, `ProcessExit`, and `SessionEnding` to attempt input release even in crash scenarios. This is best-effort — if the process is killed externally (e.g., via Task Manager), held keys may not be released until you physically press and release them.

## Debugging Tips

1. **Developer Mode**: Click "Developer Mode" in the main window to see live state, held inputs, and a scrolling event log.
2. **Hotkey conflicts**: If the toggle hotkey fails to register, another app may be using that combination. The status text will show a warning.
3. **Test with Notepad**: Open Notepad, set a repeat binding for the letter `A`, toggle on, and watch it type. Use `Esc` to stop.
4. **UAC/Admin apps**: `SendInput` cannot inject into elevated (admin) windows from a non-elevated app. Run as admin if needed.
