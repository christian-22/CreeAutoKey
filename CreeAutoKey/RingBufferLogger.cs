using System.Collections.ObjectModel;
using System.Windows.Threading;

namespace InputAutomator.Services;

/// <summary>
/// Thread-safe ring-buffer logger that exposes an ObservableCollection for WPF binding.
/// All mutations are marshalled to the UI dispatcher.
/// </summary>
public sealed class RingBufferLogger
{
    private readonly int _maxEntries;
    private readonly Dispatcher _dispatcher;

    public ObservableCollection<string> Entries { get; } = [];

    public RingBufferLogger(Dispatcher dispatcher, int maxEntries = 1000)
    {
        _dispatcher = dispatcher;
        _maxEntries = maxEntries;
    }

    public void Log(string message)
    {
        var entry = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";

        if (_dispatcher.CheckAccess())
            AddEntry(entry);
        else
            _dispatcher.BeginInvoke(() => AddEntry(entry));
    }

    private void AddEntry(string entry)
    {
        Entries.Add(entry);
        while (Entries.Count > _maxEntries)
            Entries.RemoveAt(0);
    }

    public void Clear()
    {
        if (_dispatcher.CheckAccess())
            Entries.Clear();
        else
            _dispatcher.BeginInvoke(() => Entries.Clear());
    }
}
