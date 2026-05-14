namespace LangSwitch.Core;

sealed class LayoutMonitor : IDisposable
{
    readonly System.Windows.Forms.Timer _timer;
    LayoutInfo? _current;

    public event EventHandler<LayoutChangedEventArgs>? LayoutChanged;

    public LayoutMonitor()
    {
        _timer = new System.Windows.Forms.Timer { Interval = 250 };
        _timer.Tick += OnTick;
    }

    public LayoutInfo? Current => _current;

    public void Start()
    {
        _current = WindowsLayoutDetector.GetForegroundLayout();
        _timer.Start();
    }

    void OnTick(object? sender, EventArgs e)
    {
        var next = WindowsLayoutDetector.GetForegroundLayout();
        if (next is null || next.Handle == _current?.Handle) return;

        var prev = _current;
        _current = next;
        LayoutChanged?.Invoke(this, new LayoutChangedEventArgs(prev, next));
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
    }
}

sealed class LayoutChangedEventArgs(LayoutInfo? previous, LayoutInfo current) : EventArgs
{
    public LayoutInfo? Previous { get; } = previous;
    public LayoutInfo Current { get; } = current;
}
