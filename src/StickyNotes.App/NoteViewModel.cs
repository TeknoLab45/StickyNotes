using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using StickyNotes.Core;

namespace StickyNotes.App;

public partial class NoteViewModel : ObservableObject
{
    private bool _loading;
    private DateTime _createdAt;

    public string Id { get; }

    [ObservableProperty] private string _color;
    [ObservableProperty] private string _text = "";

    /// <summary>Notun başlığı (F2); boşsa şeritte oluşturma tarihi görünür.</summary>
    [ObservableProperty] private string _noteTitle = "";

    /// <summary>Metin biçim aralıkları (kalın/altı çizili/üstü çizili).</summary>
    public IReadOnlyList<Mark> Marks { get; private set; } = Array.Empty<Mark>();

    /// <summary>Not penceresi daraltılmış mı (yalnızca başlık satırı görünür).</summary>
    [ObservableProperty] private bool _isCollapsed;

    /// <summary>Notun seçili opaklığı (0.3–1.0). Pencere fare/odak dışındayken uygulanır.</summary>
    [ObservableProperty] private double _noteOpacity = 1.0;

    /// <summary>Son diske yazılan opaklık.</summary>
    public double SavedOpacity { get; set; } = 1.0;

    /// <summary>Memos'tan gelen, bu kullanıcının düzenleyemediği not (yalnızca okunur).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditable))]
    private bool _isReadOnly;

    /// <summary>Salt okunur notun yazarı (şerit ipucunda gösterilir).</summary>
    [ObservableProperty] private string _readOnlyAuthor = "";

    public bool IsEditable => !IsReadOnly;

    /// <summary>Şeritte gösterilen başlık.</summary>
    public string Title => NoteContent.DisplayTitle(NoteTitle, _createdAt);

    /// <summary>Son diske yazılan içerik imzası (gereksiz edited_at güncellemesini önler).</summary>
    public string? SavedSignature { get; set; }

    /// <summary>İçerik ya da renk değiştiğinde tetiklenir (yükleme sırasında tetiklenmez).</summary>
    public event Action? Changed;

    public NoteViewModel(Note note)
    {
        Id = note.Id;
        _createdAt = note.CreatedAt;
        _color = note.Color;
        _noteOpacity = SavedOpacity = note.Opacity;
        LoadContent(note.Content);
        SavedSignature = _color + "|" + BuildContent();
    }

    private void LoadContent(string content)
    {
        _loading = true;
        try
        {
            NoteTitle = NoteContent.ParseTitle(content);
            Text = NoteContent.ParseText(content);
            Marks = NoteContent.ParseMarks(content);
        }
        finally { _loading = false; }
    }

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
    partial void OnIsCollapsedChanged(bool value) => Raise();
    partial void OnNoteOpacityChanged(double value) => Raise();

    partial void OnNoteTitleChanged(string value)
    {
        OnPropertyChanged(nameof(Title));
        Raise();
    }

    private void Raise()
    {
        if (_loading) return;
        Changed?.Invoke();
    }

    public string BuildContent() => NoteContent.CreateText(Text, Marks, NoteTitle);

    /// <summary>Kaydedilmemiş değişiklik var mı (diske giden içerikle karşılaştırır).</summary>
    public bool IsDirty => Color + "|" + BuildContent() != SavedSignature;

    /// <summary>Eşitlemeden gelen erişim durumu. Salt okunur not düzenlenemez; renk/opaklık/konum yine değişir.</summary>
    public void SetAccess(bool readOnly, string author)
    {
        ReadOnlyAuthor = author;
        IsReadOnly = readOnly;
    }

    /// <summary>Uzaktan güncellenmiş notu (renk, opaklık, içerik) yükler. Değişiklik olarak sayılmaz (kaydetmez).</summary>
    public void ReloadFrom(Note note)
    {
        _loading = true;
        try
        {
            _createdAt = note.CreatedAt;
            Color = note.Color;
            NoteOpacity = SavedOpacity = note.Opacity;
            LoadContent(note.Content);
        }
        finally { _loading = false; }
        SavedSignature = Color + "|" + BuildContent();
        OnPropertyChanged(nameof(Title));
    }
}
