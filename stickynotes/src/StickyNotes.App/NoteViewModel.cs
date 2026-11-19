using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using StickyNotes.Core;

namespace StickyNotes.App;

public partial class ChecklistItemViewModel : ObservableObject
{
    public string Id { get; }

    [ObservableProperty] private string _text;
    [ObservableProperty] private bool _done;
    [ObservableProperty] private int _indent;

    public ChecklistItemViewModel(ChecklistItem item)
    {
        Id = item.Id;
        _text = item.Text;
        _done = item.Done;
        _indent = item.Indent;
    }

    public ChecklistItem ToModel() => new(Id, Text, Done, Indent);
}

public partial class NoteViewModel : ObservableObject
{
    private bool _loading;

    public string Id { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsChecklist))]
    private string _type;

    [ObservableProperty] private string _color;
    [ObservableProperty] private string _text = "";

    /// <summary>Metin biçim aralıkları (kalın/altı çizili/üstü çizili); yalnızca metin notlarında dolu.</summary>
    public IReadOnlyList<Mark> Marks { get; private set; } = Array.Empty<Mark>();

    /// <summary>Not penceresi daraltılmış mı (yalnızca başlık satırı görünür).</summary>
    [ObservableProperty] private bool _isCollapsed;

    /// <summary>Notun seçili opaklığı (0.3–1.0). Pencere fare/odak dışındayken uygulanır.</summary>
    [ObservableProperty] private double _noteOpacity = 1.0;

    /// <summary>Son diske yazılan opaklık.</summary>
    public double SavedOpacity { get; set; } = 1.0;

    public ObservableCollection<ChecklistItemViewModel> Items { get; } = new();

    /// <summary>Memos'tan gelen, bu kullanıcının düzenleyemediği not (yalnızca okunur).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditable))]
    private bool _isReadOnly;

    /// <summary>Salt okunur notun yazarı (şerit ipucunda gösterilir).</summary>
    [ObservableProperty] private string _readOnlyAuthor = "";

    public bool IsEditable => !IsReadOnly;

    public bool IsChecklist => Type == NoteType.Checklist;

    /// <summary>Daraltılmış not penceresinin başlık satırında gösterilen ilk satır.</summary>
    public string Title => NoteContent.FirstLine(Type, BuildContent());

    /// <summary>Son diske yazılan içerik imzası (gereksiz edited_at güncellemesini önler).</summary>
    public string? SavedSignature { get; set; }

    /// <summary>İçerik, renk ya da tür değiştiğinde tetiklenir (yükleme sırasında tetiklenmez).</summary>
    public event Action? Changed;

    public NoteViewModel(Note note)
    {
        Id = note.Id;
        _type = note.Type;
        _color = note.Color;
        _noteOpacity = SavedOpacity = note.Opacity;
        Items.CollectionChanged += OnItemsChanged;
        LoadContent(note.Content);
        SavedSignature = _type + "|" + _color + "|" + BuildContent();
    }

    private void LoadContent(string content)
    {
        _loading = true;
        try
        {
            Text = NoteContent.ParseText(content);
            Marks = NoteContent.ParseMarks(content);
            Items.Clear();
            foreach (var i in NoteContent.ParseItems(content)) AddItemInternal(Items.Count, i);
            if (IsChecklist && Items.Count == 0) AddItemInternal(0, NoteContent.NewItem(""));
        }
        finally { _loading = false; }
    }

    private void AddItemInternal(int index, ChecklistItem model)
    {
        var vm = new ChecklistItemViewModel(model);
        vm.PropertyChanged += (_, _) => Raise();
        Items.Insert(index, vm);
    }

    public ChecklistItemViewModel InsertItem(int index, string text = "")
    {
        AddItemInternal(index, NoteContent.NewItem(text));
        return Items[index];
    }

    private void OnItemsChanged(object? s, NotifyCollectionChangedEventArgs e) => Raise();

    /// <summary>
    /// Düzenleyiciden gelen metin ve biçim aralıkları. Eski notu açmak (yalnızca CRLF→LF farkı, biçim yok)
    /// değişiklik sayılmaz; böylece edited_at değişmez.
    /// </summary>
    public void SetRich(string text, IReadOnlyList<Mark> marks)
    {
        var sameMarks = marks.SequenceEqual(Marks);
        var sameText = text == Text || (marks.Count == 0 && RichText.NormalizeNewlines(Text) == text);
        if (sameText && sameMarks) return;

        Marks = marks.ToArray();
        if (sameText) Raise();   // yalnızca biçim değişti
        else Text = text;        // OnTextChanged → Raise
    }

    partial void OnTextChanged(string value) => Raise();
    partial void OnColorChanged(string value) => Raise();
    partial void OnTypeChanged(string value) => Raise();
    partial void OnIsCollapsedChanged(bool value) => Raise();
    partial void OnNoteOpacityChanged(double value) => Raise();

    private void Raise()
    {
        if (_loading) return;
        OnPropertyChanged(nameof(Title));
        Changed?.Invoke();
    }

    /// <summary>Geçerli türe göre content JSON'u üretir.</summary>
    public string BuildContent() =>
        IsChecklist
            ? NoteContent.CreateChecklist(Items.Select(i => i.ToModel()))
            : NoteContent.CreateText(Text, Marks);

    /// <summary>Eşitlemeden gelen erişim durumu. Salt okunur not düzenlenemez; renk/opaklık/konum yine değişir.</summary>
    public void SetAccess(bool readOnly, string author)
    {
        ReadOnlyAuthor = author;
        IsReadOnly = readOnly;
    }

    /// <summary>Uzaktan güncellenmiş notu (tür, renk, opaklık, içerik) yükler. Değişiklik olarak sayılmaz (kaydetmez).</summary>
    public void ReloadFrom(Note note)
    {
        _loading = true;
        try
        {
            Type = note.Type;
            Color = note.Color;
            NoteOpacity = SavedOpacity = note.Opacity;
            LoadContent(note.Content);
        }
        finally { _loading = false; }
        SavedSignature = Type + "|" + Color + "|" + BuildContent();
        OnPropertyChanged(nameof(Title));
    }

    /// <summary>Metin ↔ checklist dönüşümü; içerik korunur.</summary>
    public void ToggleType()
    {
        if (IsReadOnly) return;
        _loading = true;
        try
        {
            if (IsChecklist)
            {
                var text = NoteContent.ItemsToText(Items.Select(i => i.ToModel()));
                Type = NoteType.Text;
                Text = text;
                Marks = Array.Empty<Mark>();   // checklist biçimi kapsam dışı; biçim düşer
            }
            else
            {
                var items = NoteContent.TextToItems(Text);
                Marks = Array.Empty<Mark>();   // metin→checklist: biçim düşer (metin korunur)
                Items.Clear();
                foreach (var i in items) AddItemInternal(Items.Count, i);
                Type = NoteType.Checklist;
            }
        }
        finally { _loading = false; }
        Raise();
    }
}
