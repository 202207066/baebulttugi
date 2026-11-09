using System.Windows;

namespace wpf;

/// <summary>Nested operations share an honest, indeterminate loading indicator.</summary>
public static class AppActivity
{
    private static readonly Dictionary<Guid, string> Active = new();
    public static event Action<string?>? Changed;
    public static string? Message { get { lock (Active) return Active.Values.LastOrDefault(); } }
    public static IDisposable Begin(string message)
    {
        var id = Guid.NewGuid();
        lock (Active) Active[id] = message;
        Publish();
        return new Operation(id);
    }
    private static void Publish()
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess()) dispatcher.BeginInvoke(() => Changed?.Invoke(Message));
        else Changed?.Invoke(Message);
    }
    private sealed class Operation(Guid id) : IDisposable
    {
        public void Dispose() { lock (Active) Active.Remove(id); Publish(); }
    }
}
