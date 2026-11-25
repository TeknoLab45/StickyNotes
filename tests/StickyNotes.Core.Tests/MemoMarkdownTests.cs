using StickyNotes.Core;
using StickyNotes.Core.Sync;

namespace StickyNotes.Core.Tests;

public class MemoMarkdownTests
{
    private static Mark M(string text, string word, string type) =>
        new(type, text.IndexOf(word, StringComparison.Ordinal), text.IndexOf(word, StringComparison.Ordinal) + word.Length);

    [Fact]
    public void Text_WithBoldAndStrike_RoundTripsAndDropsUnderline()
    {
        var text = "Alışveriş 🛒 çok önemli";
        var content = NoteContent.CreateText(text, new[] { M(text, "çok", "b"), M(text, "önemli", "s"), M(text, "Alış", "u") });

        var md = MemoMarkdown.FromNote(NoteType.Text, content);
        Assert.Equal("Alışveriş 🛒 **çok** ~~önemli~~", md);

        var (type, back) = MemoMarkdown.ToNote(md);
        Assert.Equal(NoteType.Text, type);
        Assert.Equal(text, NoteContent.ParseText(back));
        var marks = NoteContent.ParseMarks(back);
        Assert.Equal(new[] { M(text, "çok", "b"), M(text, "önemli", "s") }, marks);
    }

    [Fact]
    public void Text_WithNestedBoldAndStrike_RoundTrips()
    {
        var text = "ab cd";
        var content = NoteContent.CreateText(text, new[] { new Mark("b", 0, 5), new Mark("s", 3, 5) });
        var md = MemoMarkdown.FromNote(NoteType.Text, content);
        var (_, back) = MemoMarkdown.ToNote(md);
        Assert.Equal(text, NoteContent.ParseText(back));
        Assert.Equal(new[] { new Mark("b", 0, 5), new Mark("s", 3, 5) }, NoteContent.ParseMarks(back).OrderBy(m => m.Type).ThenBy(m => m.Start).ToList());
    }

    [Fact]
    public void Checklist_WithIndentAndDone_RoundTrips()
    {
        var items = new[]
        {
            NoteContent.NewItem("araç 34 ABC 123", done: true),
            NoteContent.NewItem("yedek parça", indent: 1),
            NoteContent.NewItem("kontrol ğüşıöç", done: false),
        };
        var content = NoteContent.CreateChecklist(items);

        var md = MemoMarkdown.FromNote(NoteType.Checklist, content);
        Assert.Equal("- [x] araç 34 ABC 123\n  - [ ] yedek parça\n- [ ] kontrol ğüşıöç", md);

        var (type, back) = MemoMarkdown.ToNote(md);
        Assert.Equal(NoteType.Checklist, type);
        var parsed = NoteContent.ParseItems(back);
        Assert.Equal(items.Select(i => (i.Text, i.Done, i.Indent)), parsed.Select(i => (i.Text, i.Done, i.Indent)));
    }

    [Fact]
    public void ForeignMarkdown_IsKeptAsPlainText()
    {
        var md = "# Başlık\n- madde\n[link](https://example.com)";
        var (type, content) = MemoMarkdown.ToNote(md);
        Assert.Equal(NoteType.Text, type);
        Assert.Equal(md, NoteContent.ParseText(content));
        Assert.Empty(NoteContent.ParseMarks(content));
        Assert.Equal(md, MemoMarkdown.FromNote(NoteType.Text, content));
    }

    [Fact]
    public void PlainText_WithoutMarks_IsByteIdenticalMarkdown()
    {
        var content = NoteContent.CreateText("satır bir\nsatır iki");
        Assert.Equal("satır bir\nsatır iki", MemoMarkdown.FromNote(NoteType.Text, content));
    }

    [Fact]
    public void Fingerprint_IgnoresTrailingWhitespace_AndTrashOnlyWhenAsked()
    {
        Assert.Equal(MemoMarkdown.Fingerprint("x", false), MemoMarkdown.Fingerprint("x\n\n", false));
        Assert.NotEqual(MemoMarkdown.Fingerprint("x", false), MemoMarkdown.Fingerprint("x", true));
    }

    [Fact]
    public void EmptyRemoteContent_BecomesEmptyText()
    {
        var (type, content) = MemoMarkdown.ToNote("  ");
        Assert.Equal(NoteType.Text, type);
        Assert.Equal("", NoteContent.ParseText(content));
    }
}
