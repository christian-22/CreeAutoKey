using System.Collections.Specialized;
using System.Windows;
using System.Windows.Input;
using InputAutomator.Models;
using InputAutomator.Services;

namespace InputAutomator.Views;

public partial class DeveloperModeWindow : Window
{
    private readonly RingBufferLogger _logger;
    private readonly SendInputInjector _injector;
    private readonly AutomationConfig _config;

    public DeveloperModeWindow(RingBufferLogger logger, SendInputInjector injector, AutomationConfig config)
    {
        InitializeComponent();
        _logger = logger;
        _injector = injector;
        _config = config;

        LogList.ItemsSource = _logger.Entries;

        // Auto-scroll to bottom
        ((INotifyCollectionChanged)_logger.Entries).CollectionChanged += (_, _) =>
        {
            if (LogList.Items.Count > 0)
                LogList.ScrollIntoView(LogList.Items[^1]);
        };

        RefreshConfig();
    }

    public void UpdateState(AutomationState state)
    {
        DevState.Text = $"State: {state}";

        var heldKeys = _injector.HeldKeys;
        var heldMouse = _injector.HeldMouseButtons;

        DevHeldKeys.Text = heldKeys.Count > 0
            ? $"Held keys: {string.Join(", ", heldKeys.Select(vk => KeyInterop.KeyFromVirtualKey(vk)))}"
            : "Held keys: (none)";

        DevHeldMouse.Text = heldMouse.Count > 0
            ? $"Held mouse: {string.Join(", ", heldMouse)}"
            : "Held mouse: (none)";
    }

    public void RefreshConfig()
    {
        var holds = _config.HoldBindings.Count > 0
            ? string.Join(", ", _config.HoldBindings.Select(b => b.DisplayName))
            : "(none)";
        var repeats = _config.RepeatBindings.Count > 0
            ? string.Join(", ", _config.RepeatBindings.Select(b => b.DisplayName))
            : "(none)";

        DevConfig.Text = $"Toggle: {_config.ToggleHotkeyDisplay}\n" +
                         $"Hold: {holds}\n" +
                         $"Repeat: {repeats}\n" +
                         $"Interval: {_config.RepeatIntervalSeconds:F3}s" +
                         (_config.HumanizeInterval ? $" (±{_config.HumanizeJitterSeconds:F3}s jitter)" : "");
    }

    private void ClearLog_Click(object sender, RoutedEventArgs e) => _logger.Clear();
}
