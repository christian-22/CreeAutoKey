using InputAutomator.Models;

namespace InputAutomator.Services;

/// <summary>
/// State machine: Idle → Countdown3 → Countdown2 → Countdown1 → Running → Idle.
/// Owns the CancellationTokenSource and orchestrates input injection.
/// </summary>
public sealed class AutomationEngine
{
    private readonly SendInputInjector _injector;
    private readonly HotkeyService _hotkeys;
    private readonly RingBufferLogger _logger;

    private CancellationTokenSource? _cts;
    private readonly object _stateLock = new();
    private readonly Random _rng = new();

    public AutomationState State { get; private set; } = AutomationState.Idle;
    public AutomationConfig Config { get; set; } = new();

    /// <summary>Fires on every state change (marshalled by caller).</summary>
    public event Action<AutomationState>? StateChanged;

    public AutomationEngine(SendInputInjector injector, HotkeyService hotkeys, RingBufferLogger logger)
    {
        _injector = injector;
        _hotkeys = hotkeys;
        _logger = logger;
    }

    /// <summary>Called by toggle hotkey or UI button.</summary>
    public void Toggle()
    {
        lock (_stateLock)
        {
            if (State == AutomationState.Idle)
                _ = StartAsync(); // fire-and-forget on the dispatcher thread
            else
                Stop("Toggle OFF");
        }
    }

    /// <summary>Begin countdown → run loop.</summary>
    public async Task StartAsync()
    {
        if (State != AutomationState.Idle) return;

        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        _hotkeys.RegisterEmergencyStops();
        _logger.Log("Automation starting – countdown begins");

        try
        {
            // Countdown 3-2-1
            foreach (var cs in new[] { AutomationState.Countdown3, AutomationState.Countdown2, AutomationState.Countdown1 })
            {
                SetState(cs);
                await Task.Delay(1000, token);
            }

            // Apply holds
            SetState(AutomationState.Running);
            _logger.Log("Applying hold bindings...");

            foreach (var binding in Config.HoldBindings)
            {
                if (binding.Kind == InputBindingKind.KeyboardKey)
                    _injector.KeyDown(binding.VirtualKey);
                else
                    _injector.MouseDown(binding.MouseButton);

                _logger.Log($"  HOLD DOWN: {binding.DisplayName}");
            }

            // Repeat loop
            if (Config.RepeatBindings.Count > 0)
            {
                _logger.Log($"Starting repeat loop (interval: {Config.RepeatIntervalSeconds:F3}s)");

                while (!token.IsCancellationRequested)
                {
                    foreach (var binding in Config.RepeatBindings)
                    {
                        if (token.IsCancellationRequested) break;

                        if (binding.Kind == InputBindingKind.KeyboardKey)
                            _injector.KeyClick(binding.VirtualKey);
                        else
                            _injector.MouseClick(binding.MouseButton);
                    }

                    double interval = Config.RepeatIntervalSeconds;
                    if (Config.HumanizeInterval && Config.HumanizeJitterSeconds > 0)
                    {
                        double jitter = (_rng.NextDouble() * 2 - 1) * Config.HumanizeJitterSeconds;
                        interval = Math.Max(0.01, interval + jitter);
                    }

                    await Task.Delay(TimeSpan.FromSeconds(interval), token);
                }
            }
            else
            {
                // No repeat bindings — just hold indefinitely
                _logger.Log("No repeat bindings. Holding until stopped.");
                await Task.Delay(Timeout.Infinite, token);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on stop
        }
        catch (Exception ex)
        {
            _logger.Log($"ERROR in automation loop: {ex.Message}");
        }
        finally
        {
            ReleaseAndReset("loop ended");
        }
    }

    /// <summary>Normal stop (toggle OFF).</summary>
    public void Stop(string reason)
    {
        _logger.Log($"Stop requested: {reason}");
        CancelAndRelease(reason);
    }

    /// <summary>Emergency stop — identical behavior, distinct log.</summary>
    public void EmergencyStop(string reason)
    {
        _logger.Log($"🚨 EMERGENCY STOP: {reason}");
        CancelAndRelease(reason);
    }

    private void CancelAndRelease(string reason)
    {
        try
        {
            _cts?.Cancel();
        }
        catch (ObjectDisposedException) { }

        ReleaseAndReset(reason);
    }

    private void ReleaseAndReset(string reason)
    {
        _injector.ReleaseAll();
        _hotkeys.UnregisterEmergencyStops();

        _cts?.Dispose();
        _cts = null;

        SetState(AutomationState.Idle);
        _logger.Log($"All inputs released. State → Idle ({reason})");
    }

    private void SetState(AutomationState newState)
    {
        State = newState;
        StateChanged?.Invoke(newState);
    }

    /// <summary>Best-effort cleanup. Call on app shutdown / crash.</summary>
    public void ForceReleaseAll()
    {
        try { _cts?.Cancel(); } catch { }
        try { _injector.ReleaseAll(); } catch { }
        try { _hotkeys.UnregisterEmergencyStops(); } catch { }
    }
}
