using System.Text.Json;

namespace Jalil.Telegram;

internal sealed class ReactionStateStore
{
    private const int MaximumEntries = 10_000;
    private readonly string _path;
    private readonly Dictionary<string, ReactionStateEntry> _entries;

    public ReactionStateStore(string path)
    {
        _path = path;
        _entries = Load(path);
    }

    public bool TryGetSelected(string key, out bool isSelected)
    {
        if (_entries.TryGetValue(key, out var entry))
        {
            isSelected = entry.IsSelected;
            return true;
        }

        isSelected = false;
        return false;
    }

    public bool SetSelected(string key, bool isSelected)
    {
        if (_entries.TryGetValue(key, out var existing) && existing.IsSelected == isSelected)
        {
            return false;
        }

        _entries[key] = new ReactionStateEntry(isSelected, DateTimeOffset.UtcNow);
        return true;
    }

    public void Save()
    {
        if (_entries.Count > MaximumEntries)
        {
            foreach (var key in _entries
                         .OrderBy(pair => pair.Value.ObservedAtUtc)
                         .Take(_entries.Count - MaximumEntries)
                         .Select(pair => pair.Key)
                         .ToArray())
            {
                _entries.Remove(key);
            }
        }

        var temporaryPath = $"{_path}.tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(_entries));
        File.Move(temporaryPath, _path, overwrite: true);
    }

    private static Dictionary<string, ReactionStateEntry> Load(string path)
    {
        if (!File.Exists(path))
        {
            return new Dictionary<string, ReactionStateEntry>(StringComparer.Ordinal);
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, ReactionStateEntry>>(
                       File.ReadAllText(path))
                   ?? new Dictionary<string, ReactionStateEntry>(StringComparer.Ordinal);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"Reaction state file is invalid: {path}",
                exception);
        }
    }

    internal sealed record ReactionStateEntry(bool IsSelected, DateTimeOffset ObservedAtUtc);
}
