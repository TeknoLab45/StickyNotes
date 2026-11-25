using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.Input;
using StickyNotes.Core;

namespace StickyNotes.App;

/// <summary>Liste satırı. ReadOnly: Memos'ta başka kullanıcının notu. Unlinked: eski Space'e bağlı (eşitlenmiyor).</summary>
public sealed record ExplorerRow(string Id, Brush Brush, string Title, string Preview, string Date, bool ReadOnly, bool Unlinked);

public partial class ExplorerWindow : Window
{
    private readonly NoteManager _manager;
    private bool _sortByCreated;
    private string? _colorFilter;
    private readonly Dictionary<string, ToggleButton> _dots = new();

    private bool IsTrash => Tabs.SelectedItem == TrashTab;

    public ExplorerWindow(NoteManager manager)
    {
        _manager = manager;
        InitializeComponent();
        InputBindings.Add(new KeyBinding(new RelayCommand(_manager.NewNote), Key.N, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new RelayCommand(() => _manager.ShowExplorer()), Key.X, ModifierKeys.Alt));
        InputBindings.Add(new KeyBinding(new RelayCommand(FocusSearch), Key.F, ModifierKeys.Control));
        BuildColorFilter();
        _manager.NotesChanged += Refresh;
        Closed += (_, _) => _manager.NotesChanged -= Refresh;
        Loaded += (_, _) => { Refresh(); SearchBox.Focus(); };
    }

    public void ShowTrash() => Tabs.SelectedItem = TrashTab;

    /// <summary>Ctrl+F: arama kutusuna odaklan ve içeriği seç.</summary>
    public void FocusSearch()
    {
        Activate();
        SearchBox.Focus();
        Keyboard.Focus(SearchBox);
        SearchBox.SelectAll();
    }

    private void BuildColorFilter()
    {
        foreach (var c in NoteColors.All)
        {
            var color = c;
            var dot = new ToggleButton
            {
                Style = (Style)FindResource("ColorDot"),
                Background = NoteTheme.Strip(color),
                ToolTip = "Yalnızca " + NoteTheme.Label(color).ToLower(),
            };
            System.Windows.Automation.AutomationProperties.SetAutomationId(dot, "FilterDot_" + color);
            System.Windows.Automation.AutomationProperties.SetName(dot, NoteTheme.Label(color));
            dot.Click += (_, _) =>
            {
                _colorFilter = dot.IsChecked == true ? color : null;
                foreach (var kv in _dots) if (kv.Key != color) kv.Value.IsChecked = false;
                Refresh();
            };
            _dots[color] = dot;
            ColorFilter.Children.Add(dot);
        }
    }

    public void Refresh()
    {
        var trash = IsTrash;
        var notes = trash ? _manager.Repo.ListTrash(SearchBox.Text) : _manager.Repo.ListActive(SearchBox.Text);
        if (_colorFilter != null) notes = notes.Where(n => n.Color == _colorFilter).ToList();
        if (!trash && _sortByCreated) notes = notes.OrderByDescending(n => n.CreatedAt).ToList();

        var now = DateTime.Now;
        var sync = _manager.Repo.ListSyncState().ToDictionary(r => r.NoteId);
        var space = _manager.Repo.LoadSyncSettings().Space;
        var rows = notes.Select(n =>
        {
            var linked = sync.TryGetValue(n.Id, out var rec);
            return new ExplorerRow(
                n.Id,
                NoteTheme.Strip(n.Color),
                NoteContent.FirstLine(n.Type, n.Content),
                NoteContent.Preview(n.Type, n.Content),
                DateText.Relative(trash ? n.DeletedAt ?? n.EditedAt : _sortByCreated ? n.CreatedAt : n.EditedAt, now),
                linked && rec!.ReadOnly,
                linked && rec!.Space != space);
        }).ToList();

        // Seçimi koru (yenilemede kaybolmasın)
        var sel = Selected().Select(r => r.Id).ToHashSet();
        List.ItemsSource = rows;
        foreach (var r in rows.Where(r => sel.Contains(r.Id))) List.SelectedItems.Add(r);

        OpenButton.Visibility = trash ? Visibility.Collapsed : Visibility.Visible;
        TrashButton.Visibility = trash ? Visibility.Collapsed : Visibility.Visible;
        RestoreButton.Visibility = trash ? Visibility.Visible : Visibility.Collapsed;
        PurgeButton.Visibility = trash ? Visibility.Visible : Visibility.Collapsed;
        EmptyButton.Visibility = trash ? Visibility.Visible : Visibility.Collapsed;
        SortButton.Visibility = trash ? Visibility.Collapsed : Visibility.Visible;

        var activeCount = _manager.Repo.ListActive().Count;
        var trashCount = _manager.Repo.ListTrash().Count;
        NotesBadge.Text = activeCount.ToString();
        TrashBadge.Text = trashCount.ToString();
        EmptyButton.IsEnabled = trashCount > 0;
        Title = $"Sticky Notes - Explorer (Notlar {activeCount} · Çöp {trashCount})";
    }

    private IEnumerable<ExplorerRow> Selected() => List.SelectedItems.OfType<ExplorerRow>().ToList();

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) { if (IsLoaded) Refresh(); }

    private void SortButton_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu { PlacementTarget = SortButton, Placement = PlacementMode.Bottom };
        foreach (var (label, byCreated) in new[] { ("Son düzenlenen", false), ("Oluşturma", true) })
        {
            var created = byCreated;
            var item = new MenuItem { Header = label, IsChecked = _sortByCreated == created };
            item.Click += (_, _) => { _sortByCreated = created; SortLabel.Text = label; Refresh(); };
            menu.Items.Add(item);
        }
        menu.IsOpen = true;
    }

    private void Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.OriginalSource == Tabs && IsLoaded) Refresh();
    }

    private void List_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (IsTrash) return;
        if (List.SelectedItem is ExplorerRow r) _manager.OpenNote(r.Id);
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        foreach (var r in Selected()) _manager.OpenNote(r.Id);
    }

    private void Trash_Click(object sender, RoutedEventArgs e)
    {
        foreach (var r in Selected()) _manager.DeleteNote(r.Id);
    }

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        foreach (var r in Selected()) _manager.RestoreNote(r.Id);
    }

    private const string MemosDeleteNote =
        "Memos ile eşlenen notlar Memos'tan da silinir (salt okunur notlar hariç; başka alana bağlı olanlar o alan yeniden seçilince).";

    private void Purge_Click(object sender, RoutedEventArgs e)
    {
        var sel = Selected().ToList();
        if (sel.Count == 0) return;
        if (!ConfirmDialog.Ask(this, "Kalıcı sil", $"{sel.Count} not kalıcı olarak silinsin mi? {MemosDeleteNote} Bu geri alınamaz.", "Kalıcı sil")) return;
        foreach (var r in sel) _manager.PurgeNote(r.Id);
    }

    private void Empty_Click(object sender, RoutedEventArgs e)
    {
        var count = _manager.Repo.ListTrash().Count;
        if (count == 0) return;
        if (!ConfirmDialog.Ask(this, "Çöpü boşalt", $"Çöpteki {count} not kalıcı olarak silinsin mi? {MemosDeleteNote} Bu geri alınamaz.", "Çöpü boşalt")) return;
        _manager.EmptyTrash();
    }
}
