namespace Warhammer.Models.Domain;

/// <summary>
/// What a unit has taken from its option groups, keyed by group id and then choice id, with a
/// count — always 1 except in a <c>select: count</c> group, where a choice may be taken several
/// times. Group ids are unique within a unit at every nesting level, so a nested group (the
/// magic standard under a standard bearer) is addressed the same way as a top-level one.
/// </summary>
public sealed class OptionSelections
{
    private readonly Dictionary<string, Dictionary<string, int>> _byGroup = new(StringComparer.Ordinal);

    /// <summary>Every group with at least one selection.</summary>
    public IEnumerable<string> GroupIds => _byGroup.Where(kv => kv.Value.Count > 0).Select(kv => kv.Key);

    public bool IsEmpty => !GroupIds.Any();

    /// <summary>The choice ids taken in a group, in the order they were taken.</summary>
    public IReadOnlyList<string> SelectedIn(string groupId) =>
        _byGroup.TryGetValue(groupId, out var picks) ? picks.Keys.ToList() : [];

    /// <summary>How many times a choice is taken; 0 when it is not.</summary>
    public int CountOf(string groupId, string choiceId) =>
        _byGroup.TryGetValue(groupId, out var picks) && picks.TryGetValue(choiceId, out var count) ? count : 0;

    public bool IsSelected(string groupId, string choiceId) => CountOf(groupId, choiceId) > 0;

    /// <summary>The one choice taken in an exclusive group, or null.</summary>
    public string? SingleIn(string groupId) => SelectedIn(groupId).FirstOrDefault();

    /// <summary>Take a choice, replacing whatever the group already held — for exclusive groups.</summary>
    public void SelectOne(string groupId, string? choiceId)
    {
        if (choiceId is null)
        {
            Clear(groupId);
            return;
        }

        _byGroup[groupId] = new Dictionary<string, int>(StringComparer.Ordinal) { [choiceId] = 1 };
    }

    /// <summary>Set a choice's count in a group; 0 removes it. Other choices in the group are untouched.</summary>
    public void Set(string groupId, string choiceId, int count)
    {
        if (count <= 0)
        {
            Remove(groupId, choiceId);
            return;
        }

        if (!_byGroup.TryGetValue(groupId, out var picks))
            _byGroup[groupId] = picks = new Dictionary<string, int>(StringComparer.Ordinal);
        picks[choiceId] = count;
    }

    public void Toggle(string groupId, string choiceId, bool selected) =>
        Set(groupId, choiceId, selected ? 1 : 0);

    public void Remove(string groupId, string choiceId)
    {
        if (!_byGroup.TryGetValue(groupId, out var picks)) return;
        picks.Remove(choiceId);
        if (picks.Count == 0) _byGroup.Remove(groupId);
    }

    public void Clear(string groupId) => _byGroup.Remove(groupId);

    public void ClearAll() => _byGroup.Clear();

    /// <summary>A plain copy for saving and duplicating: group id → choice id → count.</summary>
    public Dictionary<string, Dictionary<string, int>> ToDictionary() =>
        _byGroup.Where(kv => kv.Value.Count > 0)
            .ToDictionary(kv => kv.Key, kv => new Dictionary<string, int>(kv.Value));

    public void CopyFrom(OptionSelections other)
    {
        _byGroup.Clear();
        foreach (var (group, picks) in other._byGroup)
            _byGroup[group] = new Dictionary<string, int>(picks, StringComparer.Ordinal);
    }

    public void CopyFrom(IReadOnlyDictionary<string, Dictionary<string, int>>? saved)
    {
        _byGroup.Clear();
        if (saved is null) return;
        foreach (var (group, picks) in saved)
            foreach (var (choice, count) in picks)
                Set(group, choice, count);
    }
}
