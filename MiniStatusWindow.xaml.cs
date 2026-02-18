using System.Windows;
using System.Windows.Media;
using InputAutomator.Models;

namespace InputAutomator.Views;

public partial class MiniStatusWindow : Window
{
    public MiniStatusWindow()
    {
        InitializeComponent();
    }

    public void UpdateState(AutomationState state)
    {
        var (text, color) = state switch
        {
            AutomationState.Countdown3 => ("3", Brushes.Orange),
            AutomationState.Countdown2 => ("2", Brushes.Orange),
            AutomationState.Countdown1 => ("1", Brushes.OrangeRed),
            AutomationState.Running    => ("ON", Brushes.LimeGreen),
            _                          => ("OFF", new SolidColorBrush(Color.FromRgb(0xA6, 0xAD, 0xC8)))
        };

        StateText.Text = text;
        StateText.Foreground = color;
    }

    public void UpdateToggleDisplay(string hotkeyText)
    {
        ToggleInfo.Text = $"Toggle: {hotkeyText}";
    }
}
