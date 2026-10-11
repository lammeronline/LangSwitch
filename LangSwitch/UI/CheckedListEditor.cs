namespace LangSwitch.UI;

/// <summary>
/// Shared "checked list + add/remove + reentrancy guard" wiring for a WinForms
/// CheckedListBox-backed master list. The Abbreviations and Dictionaries sections of
/// Settings both hand-rolled this exact ~30-line pattern (ItemCheck toggling a model
/// property, a suppress-flag guard against TextChanged handlers firing while the list/detail
/// controls are being repopulated programmatically, Add/Remove/Load) with only the item type
/// and its Enabled accessor differing — this factors that out. The detail pane (the fields
/// shown for the selected item) stays owned by the caller, since those differ per section.
/// </summary>
sealed class CheckedListEditor<T> where T : class
{
    readonly CheckedListBox _listBox;
    readonly Func<T, bool> _getEnabled;
    readonly Action<T, bool> _setEnabled;

    /// <summary>True while the list is being repopulated programmatically. Exposed so a
    /// caller's own detail-field handlers (TextChanged etc.) can check it directly, though
    /// <see cref="WhenSelected"/> already does that for the common case.</summary>
    public bool Suppress { get; private set; }

    public T? SelectedItem => _listBox.SelectedItem as T;

    public CheckedListEditor(CheckedListBox listBox, Func<T, bool> getEnabled, Action<T, bool> setEnabled)
    {
        _listBox = listBox;
        _getEnabled = getEnabled;
        _setEnabled = setEnabled;
        _listBox.ItemCheck += (_, e) =>
        {
            if (Suppress) return;
            if (_listBox.Items[e.Index] is T item) _setEnabled(item, e.NewValue == CheckState.Checked);
        };
    }

    public void LoadList(IEnumerable<T> items)
    {
        WithSuppressed(() =>
        {
            _listBox.Items.Clear();
            foreach (var item in items)
            {
                int idx = _listBox.Items.Add(item);
                _listBox.SetItemChecked(idx, _getEnabled(item));
            }
        });
        if (_listBox.Items.Count > 0) _listBox.SelectedIndex = 0;
    }

    /// <summary>Adds a new item to the list box (checked, selected) — the caller is
    /// responsible for adding it to its own backing collection first.</summary>
    public void Add(T item)
    {
        int idx = 0;
        WithSuppressed(() =>
        {
            idx = _listBox.Items.Add(item);
            _listBox.SetItemChecked(idx, true);
        });
        _listBox.SelectedIndex = idx;
    }

    /// <summary>Removes the currently selected item from the list box (selecting a neighbor
    /// if any remain, which fires SelectedIndexChanged) and returns it, or null if nothing
    /// was selected. The caller is responsible for removing it from its own backing
    /// collection, and for refreshing its detail pane itself if the list is now empty (no
    /// selection-changed event fires in that case).</summary>
    public T? RemoveSelected()
    {
        int idx = _listBox.SelectedIndex;
        if (idx < 0) return null;
        var item = _listBox.Items[idx] as T;
        _listBox.Items.RemoveAt(idx);
        if (_listBox.Items.Count > 0)
            _listBox.SelectedIndex = Math.Min(idx, _listBox.Items.Count - 1);
        return item;
    }

    /// <summary>Runs <paramref name="apply"/> against the selected item, unless the list is
    /// mid-repopulation or nothing is selected — mirrors the guard every detail field's own
    /// TextChanged handler previously repeated by hand.</summary>
    public void WhenSelected(Action<T> apply)
    {
        if (Suppress || SelectedItem is not { } item) return;
        apply(item);
    }

    /// <summary>Runs <paramref name="action"/> with Suppress set, for callers that need to
    /// push the selected item's data into detail controls without those controls' own
    /// TextChanged handlers writing it straight back.</summary>
    public void WithSuppressed(Action action)
    {
        Suppress = true;
        try { action(); }
        finally { Suppress = false; }
    }

    /// <summary>Re-renders the list box's visible text for its rows (call after editing a
    /// field whose value appears in a row's ToString()).</summary>
    public void RefreshRows() => _listBox.Refresh();
}
