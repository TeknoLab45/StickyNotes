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
    public void LegacyChecklist_BecomesMarkdownTaskList()
    {
        var content = NoteContent.CreateChecklist(new[]
        {
            NoteContent.NewItem("araç 34 ABC 123", done: true),
            NoteContent.NewItem("yedek parça", indent: 1),
        });
        var md = MemoMarkdown.FromNote(NoteType.Checklist, content);
        Assert.Equal("- [x] araç 34 ABC 123\n  - [ ] yedek parça", md);

        var (type, back) = MemoMarkdown.ToNote(md);
        Assert.Equal(NoteType.Text, type);
        Assert.Equal(md, NoteContent.ParseText(back));
    }

    [Fact]
    public void Title_IsFirstHeadingLine_BothWays()
    {
        var content = NoteContent.CreateText("- süt\n- ekmek", title: "Alışveriş");
        var md = MemoMarkdown.FromNote(NoteType.Text, content);
        Assert.Equal("# Alışveriş\n- süt\n- ekmek", md);
        Assert.Equal(content, MemoMarkdown.ToNote(md).Content);

        Assert.Equal("# Yalnız başlık", MemoMarkdown.FromNote(NoteType.Text, NoteContent.CreateText("", title: "Yalnız başlık")));
    }

    [Theory]
    [InlineData("düz ilk satır\nikinci")]
    [InlineData("#etiket\nmetin")]
    [InlineData("## alt başlık\nmetin")]
    [InlineData("metin\n# sonraki başlık")]
    public void MemoWithoutTopHeading_HasNoTitle(string md)
    {
        var (_, content) = MemoMarkdown.ToNote(md);
        Assert.Equal("", NoteContent.ParseTitle(content));
        Assert.Equal(md, MemoMarkdown.FromNote(NoteType.Text, content));
    }

    [Theory]
    [InlineData("# Başlık\n\ngövde")]
    [InlineData("- madde\n  - alt\n1. bir\n- [ ] görev")]
    [InlineData("2 ** 3 ** 4")]
    [InlineData("** boşluklu **")]
    [InlineData("***üçlü***")]
    [InlineData("**- liste değil**")]
    [InlineData("- **kalın madde** devam ~~çizili~~")]
    public void Markdown_RoundTripsExactly(string md)
    {
        var (type, content) = MemoMarkdown.ToNote(md);
        Assert.Equal(md, MemoMarkdown.FromNote(type, content));
    }

    [Fact]
    public void Bold_WithSurroundingSpacesAndListMarker_StaysValidMarkdown()
    {
        // Yazarken kalın modu: boşluk ve liste işareti de kalın kaydedilebilir; markdown'da işaretlerin dışında kalır.
        var content = NoteContent.CreateText("- merhaba dünya ", new[] { new Mark("b", 0, 10), new Mark("b", 15, 16) });
        Assert.Equal("- **merhaba** dünya ", MemoMarkdown.FromNote(NoteType.Text, content));

        var nested = NoteContent.CreateText("ab cd ef", new[] { new Mark("b", 0, 6), new Mark("s", 3, 8) });
        Assert.Equal("**ab ~~cd~~** ~~ef~~", MemoMarkdown.FromNote(NoteType.Text, nested));
    }

    [Fact]
    public void ForeignMarkdown_IsKeptAsPlainText()
    {
        var md = "giriş\n# Ara başlık\n- madde\n[link](https://example.com)";
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
