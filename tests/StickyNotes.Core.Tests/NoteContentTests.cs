using StickyNotes.Core;
namespace StickyNotes.Core.Tests;

public class NoteContentTests
{
    [Fact]
    public void Text_RoundTrip_PreservesUnicodeAndNewlines()
    {
        var c = NoteContent.CreateText("Merhaba dünya\nİkinci \"satır\"");
        Assert.Equal("Merhaba dünya\nİkinci \"satır\"", NoteContent.ParseText(c));
        Assert.Contains("\"v\":1", c);
    }

    [Fact]
    public void Checklist_RoundTrip_KeepsFields()
    {
        var items = new[] { new ChecklistItem("a", "süt", true, 0), new ChecklistItem("b", "ekmek", false, 1) };
        var parsed = NoteContent.ParseItems(NoteContent.CreateChecklist(items));
        Assert.Equal(items, parsed);
    }

    [Fact]
    public void TextToChecklist_SplitsLines()
    {
        var c = NoteContent.ToChecklist(NoteContent.CreateText("bir\niki\nüç"));
        var items = NoteContent.ParseItems(c);
        Assert.Equal(new[] { "bir", "iki", "üç" }, items.Select(i => i.Text));
        Assert.All(items, i => Assert.False(i.Done));
    }

    [Fact]
    public void ChecklistToText_JoinsLines()
    {
        var c = NoteContent.CreateChecklist(new[] { NoteContent.NewItem("x", true), NoteContent.NewItem("y") });
        Assert.Equal("x\ny", NoteContent.ParseText(NoteContent.ToText(c)));
    }

    [Fact]
    public void EmptyText_ToChecklist_GivesOneEmptyItem()
    {
        var items = NoteContent.ParseItems(NoteContent.ToChecklist(NoteContent.CreateText("")));
        Assert.Single(items);
        Assert.Equal("", items[0].Text);
    }

    [Fact]
    public void InvalidJson_DoesNotThrow()
    {
        Assert.Equal("", NoteContent.ParseText("not json"));
        Assert.Empty(NoteContent.ParseItems("{bad"));
    }

    [Fact]
    public void FirstLine_SkipsBlankLines()
    {
        Assert.Equal("başlık", NoteContent.FirstLine(NoteType.Text, NoteContent.CreateText("\n\n  başlık \nx")));
        Assert.Equal("(boş not)", NoteContent.FirstLine(NoteType.Text, NoteContent.CreateText("")));
    }

    [Fact]
    public void Item_Ids_AreUnique()
    {
        Assert.NotEqual(NoteContent.NewItem("a").Id, NoteContent.NewItem("a").Id);
    }

    [Fact]
    public void Preview_Text_IsRestAfterFirstLine()
    {
        Assert.Equal("ikinci · üçüncü", NoteContent.Preview(NoteType.Text, NoteContent.CreateText("başlık\n\nikinci\n üçüncü ")));
        Assert.Equal("", NoteContent.Preview(NoteType.Text, NoteContent.CreateText("tek satır")));
    }

    [Fact]
    public void Preview_Checklist_ShowsProgress()
    {
        var content = NoteContent.CreateChecklist(new[]
        {
            NoteContent.NewItem("a", true), NoteContent.NewItem("b"), NoteContent.NewItem("c", true),
            NoteContent.NewItem("d"), NoteContent.NewItem("e"), NoteContent.NewItem("")
        });
        Assert.Equal("2/5 tamam", NoteContent.Preview(NoteType.Checklist, content));
        Assert.Equal("", NoteContent.Preview(NoteType.Checklist, NoteContent.Empty(NoteType.Checklist)));
    }

    [Fact]
    public void DateText_Relative()
    {
        var now = new DateTime(2026, 11, 17, 20, 0, 0, DateTimeKind.Local);
        Assert.Equal("bugün 18:05", DateText.Relative(new DateTime(2026, 11, 17, 18, 5, 0, DateTimeKind.Local).ToUniversalTime(), now));
        Assert.Equal("dün 09:12", DateText.Relative(new DateTime(2026, 11, 16, 9, 12, 0, DateTimeKind.Local).ToUniversalTime(), now));
        Assert.Equal("01.11.2026 10:00", DateText.Relative(new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Local).ToUniversalTime(), now));
    }
}
