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
    public void InvalidJson_DoesNotThrow()
    {
        Assert.Equal("", NoteContent.ParseText("not json"));
        Assert.Empty(NoteContent.ParseItems("{bad"));
    }

    [Fact]
    public void Title_IsStoredSeparately_AndOptional()
    {
        var c = NoteContent.CreateText("gövde", title: "  Başlık\r\niki  ");
        Assert.Equal("Başlık iki", NoteContent.ParseTitle(c));
        Assert.Equal("gövde", NoteContent.ParseText(c));
        Assert.Equal("Başlık iki\ngövde", NoteContent.PlainText(NoteType.Text, c));
        Assert.Equal("{\"v\":1,\"text\":\"gövde\"}", NoteContent.CreateText("gövde", title: "   "));
        Assert.Equal("", NoteContent.ParseTitle("{bad"));
    }

    [Fact]
    public void DisplayTitle_FallsBackToCreationDate()
    {
        var utc = new DateTime(2026, 11, 17, 18, 5, 0, DateTimeKind.Local).ToUniversalTime();
        Assert.Equal("Plan", NoteContent.DisplayTitle("Plan", utc));
        Assert.Equal("17.11.2026 18:05", NoteContent.DisplayTitle("", utc));
    }

    [Fact]
    public void Item_Ids_AreUnique()
    {
        Assert.NotEqual(NoteContent.NewItem("a").Id, NoteContent.NewItem("a").Id);
    }

    [Fact]
    public void Preview_JoinsNonEmptyLines()
    {
        Assert.Equal("ilk · ikinci", NoteContent.Preview(NoteContent.CreateText("ilk\n\n ikinci ")));
        Assert.Equal("", NoteContent.Preview(NoteContent.CreateText("")));
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
