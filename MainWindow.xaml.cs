using System.Windows;
using System.Windows.Input;
using InputAutomator.Models;
using InputAutomator.Services;
using Binding = InputAutomator.Models.InputBinding;

namespace InputAutomator.Views;

public partial class MainWindow : Window
{
    private readonly AutomationConfig _config;
    private readonly AutomationEngine _engine;
    private readonly HotkeyService _hotkeys;
    private readonly RingBufferLogger _logger;
    private readonly SendInputInjector _injector;
    private readonly MiniStatusWindow _miniStatus;

    private DeveloperModeWindow? _devWindow;

    // Key-capture state
    private enum CaptureTarget { None, Hotkey, HoldKey, RepeatKey }
    private CaptureTarget _captureTarget = CaptureTarget.None;

    public MainWindow()
    {
        InitializeComponent();

        // Bootstrap services
        _config = ConfigStore.Load();
        _logger = new RingBufferLogger(Dispatcher);
        _injector = new SendInputInjector();
        _hotkeys = new HotkeyService(_logger);
        _engine = new AutomationEngine(_injector, _hotkeys, _logger) { Config = _config };

        // Wire events
        _engine.StateChanged += OnStateChanged;
        _hotkeys.TogglePressed += () => Dispatcher.Invoke(() => _engine.Toggle());
        _hotkeys.EmergencyStopPressed += reason => Dispatcher.Invoke(() => _engine.EmergencyStop(reason));

        // Mini status window
        _miniStatus = new MiniStatusWindow();
        _miniStatus.Show();

        // Load config into UI
        ApplyConfigToUI();

        // Register hotkeys after window handle is available
        SourceInitialized += (_, _) =>
        {
            _hotkeys.Initialize(this);
            RegisterCurrentToggle();
        };

        _logger.Log("Application started.");

        // Register injector with App for crash-path safety
        if (Application.Current is App app)
            app.RegisterInjector(_injector);
    }

    // ── State Changes ──────────────────────────────────────────────

    private void OnStateChanged(AutomationState state)
    {
        Dispatcher.Invoke(() =>
        {
            _miniStatus.UpdateState(state);
            _devWindow?.UpdateState(state);
        });
    }

    // ── Config → UI ────────────────────────────────────────────────

    private void ApplyConfigToUI()
    {
        HotkeyBox.Text = _config.ToggleHotkeyDisplay;
        IntervalBox.Text = _config.RepeatIntervalSeconds.ToString("F2");
        HumanizeCheck.IsChecked = _config.HumanizeInterval;
        JitterBox.Text = _config.HumanizeJitterSeconds.ToString("F3");

        RefreshHoldList();
        RefreshRepeatList();
        _miniStatus.UpdateToggleDisplay(_config.ToggleHotkeyDisplay);
    }

    private void RefreshHoldList()
    {
        HoldList.ItemsSource = null;
        HoldList.ItemsSource = _config.HoldBindings.Select(b => b.DisplayName).ToList();
    }

    private void RefreshRepeatList()
    {
        RepeatList.ItemsSource = null;
        RepeatList.ItemsSource = _config.RepeatBindings.Select(b => b.DisplayName).ToList();
    }

    private void SaveConfig()
    {
        ConfigStore.Save(_config);
        _devWindow?.RefreshConfig();
    }

    // ── Toggle Hotkey Picker ───────────────────────────────────────

    private void HotkeyBox_GotFocus(object sender, RoutedEventArgs e)
    {
        _captureTarget = CaptureTarget.Hotkey;
        HotkeyBox.Text = "(press hotkey combo...)";
        HotkeyStatus.Text = "";
    }

    private void HotkeyBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_captureTarget == CaptureTarget.Hotkey)
        {
            _captureTarget = CaptureTarget.None;
            HotkeyBox.Text = _config.ToggleHotkeyDisplay;
        }
    }

    private void HotkeyBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_captureTarget != CaptureTarget.Hotkey) return;

        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
            or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin)
            return; // Wait for the actual key

        var mods = Keyboard.Modifiers;
        _config.ToggleModifiers = mods;
        _config.ToggleKey = key;
        _captureTarget = CaptureTarget.None;

        HotkeyBox.Text = _config.ToggleHotkeyDisplay;
        _miniStatus.UpdateToggleDisplay(_config.ToggleHotkeyDisplay);

        if (!RegisterCurrentToggle())
            HotkeyStatus.Text = "⚠ Failed to register. Key may be in use by another app.";
        else
            HotkeyStatus.Text = "";

        SaveConfig();

        // Move focus away
        ToggleBtn.Focus();
    }

    private bool RegisterCurrentToggle()
    {
        return _hotkeys.RegisterToggle(_config.ToggleModifiers, _config.ToggleKey);
    }

    // ── Key Capture for Hold/Repeat ────────────────────────────────

    private void AddHoldKey_Click(object sender, RoutedEventArgs e) => StartKeyCapture(CaptureTarget.HoldKey);
    private void AddRepeatKey_Click(object sender, RoutedEventArgs e) => StartKeyCapture(CaptureTarget.RepeatKey);

    private void StartKeyCapture(CaptureTarget target)
    {
        if (_engine.State != AutomationState.Idle)
        {
            MessageBox.Show("Stop automation before editing bindings.", "Busy", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _captureTarget = target;

        var win = new Window
        {
            Title = "Press a key…",
            Width = 250,
            Height = 100,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1E, 0x1E, 0x2E)),
            Content = new System.Windows.Controls.TextBlock
            {
                Text = "Press any key to bind it…",
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };

        win.PreviewKeyDown += (_, ke) =>
        {
            ke.Handled = true;
            var key = ke.Key == Key.System ? ke.SystemKey : ke.Key;
            if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
                or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin)
                return;

            var binding = Binding.FromKey(key);
            AddBindingToTarget(target, binding);
            win.Close();
        };

        win.ShowDialog();
        _captureTarget = CaptureTarget.None;
    }

    private void AddBindingToTarget(CaptureTarget target, Binding binding)
    {
        var list = target == CaptureTarget.HoldKey ? _config.HoldBindings : _config.RepeatBindings;
        if (!list.Contains(binding))
        {
            list.Add(binding);
            _logger.Log($"Added {(target == CaptureTarget.HoldKey ? "hold" : "repeat")} binding: {binding.DisplayName}");
        }

        if (target == CaptureTarget.HoldKey) RefreshHoldList();
        else RefreshRepeatList();
        SaveConfig();
    }

    // ── Mouse Button Bindings ──────────────────────────────────────

    private void AddHoldMouseLeft_Click(object sender, RoutedEventArgs e) => AddBindingToTarget(CaptureTarget.HoldKey, Binding.FromMouse(MouseBtn.Left));
    private void AddHoldMouseRight_Click(object sender, RoutedEventArgs e) => AddBindingToTarget(CaptureTarget.HoldKey, Binding.FromMouse(MouseBtn.Right));
    private void AddHoldMouseMiddle_Click(object sender, RoutedEventArgs e) => AddBindingToTarget(CaptureTarget.HoldKey, Binding.FromMouse(MouseBtn.Middle));
    private void AddRepeatMouseLeft_Click(object sender, RoutedEventArgs e) => AddBindingToTarget(CaptureTarget.RepeatKey, Binding.FromMouse(MouseBtn.Left));
    private void AddRepeatMouseRight_Click(object sender, RoutedEventArgs e) => AddBindingToTarget(CaptureTarget.RepeatKey, Binding.FromMouse(MouseBtn.Right));
    private void AddRepeatMouseMiddle_Click(object sender, RoutedEventArgs e) => AddBindingToTarget(CaptureTarget.RepeatKey, Binding.FromMouse(MouseBtn.Middle));

    // ── Remove Bindings ────────────────────────────────────────────

    private void RemoveHold_Click(object sender, RoutedEventArgs e)
    {
        var idx = HoldList.SelectedIndex;
        if (idx >= 0 && idx < _config.HoldBindings.Count)
        {
            _logger.Log($"Removed hold binding: {_config.HoldBindings[idx].DisplayName}");
            _config.HoldBindings.RemoveAt(idx);
            RefreshHoldList();
            SaveConfig();
        }
    }

    private void RemoveRepeat_Click(object sender, RoutedEventArgs e)
    {
        var idx = RepeatList.SelectedIndex;
        if (idx >= 0 && idx < _config.RepeatBindings.Count)
        {
            _logger.Log($"Removed repeat binding: {_config.RepeatBindings[idx].DisplayName}");
            _config.RepeatBindings.RemoveAt(idx);
            RefreshRepeatList();
            SaveConfig();
        }
    }

    // ── Interval ───────────────────────────────────────────────────

    private void IntervalBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (double.TryParse(IntervalBox.Text, out double val))
        {
            _config.RepeatIntervalSeconds = Math.Clamp(val, 0.01, 3600);
            SaveConfig();
        }
    }

    private void JitterBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (double.TryParse(JitterBox.Text, out double val))
        {
            _config.HumanizeJitterSeconds = Math.Clamp(val, 0.001, 10);
            SaveConfig();
        }
    }

    private void Humanize_Changed(object sender, RoutedEventArgs e)
    {
        _config.HumanizeInterval = HumanizeCheck.IsChecked == true;
        SaveConfig();
    }

    // ── Toggle Button ──────────────────────────────────────────────

    private void ToggleBtn_Click(object sender, RoutedEventArgs e) => _engine.Toggle();

    // ── Developer Mode ─────────────────────────────────────────────

    private void DevMode_Click(object sender, RoutedEventArgs e)
    {
        if (_devWindow is null || !_devWindow.IsLoaded)
        {
            _devWindow = new DeveloperModeWindow(_logger, _injector, _config);
            _devWindow.UpdateState(_engine.State);
            _devWindow.Show();
        }
        else
        {
            _devWindow.Activate();
        }
    }

    // ── Closing ────────────────────────────────────────────────────

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        _engine.ForceReleaseAll();
        _hotkeys.Dispose();
        _miniStatus.Close();
        _devWindow?.Close();
        ConfigStore.Save(_config);
    }
}
