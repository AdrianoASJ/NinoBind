using System.Collections.ObjectModel;

namespace NinoBind.Hosting;

public sealed record ActivityItem(string Glyph, string Text, DateTimeOffset At);

public sealed class ActivityFeed
{
    public static ActivityFeed Current { get; } = new();

    public ObservableCollection<ActivityItem> Items { get; } = [];

    public void Add(string glyph, string text)
    {
        Items.Insert(0, new ActivityItem(glyph, text, DateTimeOffset.Now));
        while (Items.Count > 20)
            Items.RemoveAt(Items.Count - 1);
    }
}
