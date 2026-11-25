using StickyNotes.Core;
namespace StickyNotes.Core.Tests;

public class RichTextTests
{
    private static Mark M(string t, int s, int e) => new(t, s, e);

    [Fact]
    public void Normalize_MergesOverlappingAndAdjacent_SameType()
    {
        var r = RichText.Normalize(new[] { M("b", 0, 3), M("b", 2, 5), M("b", 5, 7) }, 10);
        Assert.Equal(new[] { M("b", 0, 7) }, r);
    }

    [Fact]
    public void Normalize_ClipsToTextLength_AndDropsEmptyOrReversed()
    {
        var r = RichText.Normalize(new[] { M("u", -4, 3), M("u", 8, 40), M("u", 5, 5), M("u", 9, 2) }, 10);
        Assert.Equal(new[] { M("u", 0, 3), M("u", 8, 10) }, r);
    }

    [Fact]
    public void Normalize_DropsUnknownType()
    {
        Assert.Empty(RichText.Normalize(new[] { M("i", 0, 3) }, 10));
    }

    [Fact]
    public void Normalize_DifferentTypes_MayOverlap_AndAreSortedByTypeThenStart()
    {
        var r = RichText.Normalize(new[] { M("u", 2, 6), M("b", 0, 4), M("b", 6, 8) }, 10);
        Assert.Equal(new[] { M("b", 0, 4), M("b", 6, 8), M("u", 2, 6) }, r);
    }

    [Fact]
    public void Toggle_AddsMark_WhenNothingMarked()
    {
        var r = RichText.Toggle(new List<Mark>(), "b", 2, 5, 10);
        Assert.Equal(new[] { M("b", 2, 5) }, r);
    }

    [Fact]
    public void Toggle_RemovesMark_WhenFullyCovered()
    {
        var r = RichText.Toggle(new[] { M("b", 0, 10) }, "b", 2, 5, 10);
        Assert.Equal(new[] { M("b", 0, 2), M("b", 5, 10) }, r);
    }

    [Fact]
    public void Toggle_RemovesMiddle_SplitsIntoTwo()
    {
        var r = RichText.Toggle(new[] { M("u", 1, 9) }, "u", 4, 6, 10);
        Assert.Equal(new[] { M("u", 1, 4), M("u", 6, 9) }, r);
    }

    [Fact]
    public void Toggle_PartiallyCovered_AddsWholeRange_AndMerges()
    {
        var r = RichText.Toggle(new[] { M("b", 0, 3) }, "b", 2, 6, 10);
        Assert.Equal(new[] { M("b", 0, 6) }, r);
    }

    [Fact]
    public void Toggle_EmptyRange_ReturnsUnchanged()
    {
        var input = new[] { M("b", 1, 4) };
        Assert.Equal(input, RichText.Toggle(input, "b", 3, 3, 10));
        Assert.Equal(input, RichText.Toggle(input, "s", 3, 3, 10));
    }

    [Fact]
    public void Toggle_OtherTypes_AreNotTouched()
    {
        var input = new[] { M("u", 0, 10) };
        var r = RichText.Toggle(input, "s", 2, 5, 10);
        Assert.Contains(M("u", 0, 10), r);
        Assert.Contains(M("s", 2, 5), r);
        var removed = RichText.Toggle(r, "s", 2, 5, 10);
        Assert.Equal(new[] { M("u", 0, 10) }, removed);
    }

    [Fact]
    public void IsFullyMarked_EndIsExclusive()
    {
        var marks = new[] { M("b", 2, 5) };
        Assert.True(RichText.IsFullyMarked(marks, "b", 2, 5));
        Assert.True(RichText.IsFullyMarked(marks, "b", 3, 4));
        Assert.False(RichText.IsFullyMarked(marks, "b", 2, 6));
        Assert.False(RichText.IsFullyMarked(marks, "u", 2, 5));
    }

    [Fact]
    public void IsFullyMarked_EmptyRange_IsFalse()
    {
        Assert.False(RichText.IsFullyMarked(new[] { M("b", 0, 5) }, "b", 2, 2));
    }

    [Fact]
    public void IsFullyMarked_AcrossAdjacentMarks_IsTrue()
    {
        Assert.True(RichText.IsFullyMarked(new[] { M("b", 0, 3), M("b", 3, 6) }, "b", 1, 5));
    }

    [Fact]
    public void TypesAt_ReturnsAllCoveringTypes_EndExcluded()
    {
        var marks = new[] { M("b", 0, 4), M("u", 2, 6), M("s", 5, 7) };
        static string At(IReadOnlyList<Mark> m, int i) => string.Join(",", RichText.TypesAt(m, i).OrderBy(t => t));
        Assert.Equal("b", At(marks, 0));
        Assert.Equal("b,u", At(marks, 2));
        Assert.Equal("u", At(marks, 4));
        Assert.Equal("s,u", At(marks, 5));
        Assert.Equal("s", At(marks, 6));
        Assert.Equal("", At(marks, 7));
    }

    [Fact]
    public void NormalizeNewlines_ConvertsCrLfAndCr()
    {
        Assert.Equal("a\nb\nc\n", RichText.NormalizeNewlines("a\r\nb\rc\n"));
    }
}

public class NoteContentRichTextTests
{
    private static Mark M(string t, int s, int e) => new(t, s, e);

    [Fact]
    public void CreateText_WithoutMarks_IsByteIdenticalToLegacyFormat()
    {
        const string legacy = "{\"v\":1,\"text\":\"x\"}";
        Assert.Equal(legacy, NoteContent.CreateText("x"));
        Assert.Equal(legacy, NoteContent.CreateText("x", Array.Empty<Mark>()));
        Assert.Equal(legacy, NoteContent.CreateText("x", null));
    }

    [Fact]
    public void Marks_RoundTrip()
    {
        var text = "Toplantı yarın saat 10";
        var content = NoteContent.CreateText(text, new[] { M("b", 0, 8), M("u", 15, 22) });
        Assert.Equal(text, NoteContent.ParseText(content));
        Assert.Equal(new[] { M("b", 0, 8), M("u", 15, 22) }, NoteContent.ParseMarks(content));
    }

    [Fact]
    public void CreateText_NormalizesMarksBeforeWriting()
    {
        var content = NoteContent.CreateText("abcdef", new[] { M("b", 4, 99), M("b", 0, 2), M("b", 1, 3) });
        Assert.Equal(new[] { M("b", 0, 3), M("b", 4, 6) }, NoteContent.ParseMarks(content));
    }

    [Fact]
    public void LegacyContent_HasNoMarks()
    {
        Assert.Empty(NoteContent.ParseMarks("{\"v\":1,\"text\":\"eski not\"}"));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[1,2,3]")]
    [InlineData("\"yalnız string\"")]
    [InlineData("{\"v\":1,\"text\":\"abc\",\"marks\":\"bozuk\"}")]
    [InlineData("{\"v\":1,\"text\":\"abc\",\"marks\":[\"x\",3,null]}")]
    [InlineData("{\"v\":1,\"text\":\"abc\",\"marks\":[{\"t\":\"b\"},{\"s\":0,\"e\":1},{\"t\":5,\"s\":0,\"e\":1}]}")]
    [InlineData("{\"v\":1,\"text\":\"abc\",\"marks\":[{\"t\":\"b\",\"s\":\"0\",\"e\":1.5}]}")]
    public void BrokenMarks_DoNotThrow(string content)
    {
        var ex = Record.Exception(() => NoteContent.ParseMarks(content));
        Assert.Null(ex);
    }

    [Fact]
    public void NegativeAndOverlongMarks_AreClipped()
    {
        var content = "{\"v\":1,\"text\":\"abc\",\"marks\":[{\"t\":\"b\",\"s\":-5,\"e\":2},{\"t\":\"u\",\"s\":1,\"e\":900}]}";
        Assert.Equal(new[] { M("b", 0, 2), M("u", 1, 3) }, NoteContent.ParseMarks(content));
    }

    [Fact]
    public void MarksWithoutText_AreDropped()
    {
        Assert.Empty(NoteContent.ParseMarks("{\"v\":1,\"marks\":[{\"t\":\"b\",\"s\":0,\"e\":2}]}"));
    }

    [Fact]
    public void PlainTextHelpers_IgnoreMarks()
    {
        var content = NoteContent.CreateText("başlık\nikinci", new[] { M("b", 0, 6), M("s", 7, 13) });
        Assert.Equal("başlık\nikinci", NoteContent.PlainText(NoteType.Text, content));
        Assert.Equal("başlık", NoteContent.FirstLine(NoteType.Text, content));
        Assert.Equal("ikinci", NoteContent.Preview(NoteType.Text, content));
    }

    [Fact]
    public void ToChecklist_DropsMarks()
    {
        var content = NoteContent.CreateText("bir\niki", new[] { M("b", 0, 3) });
        var checklist = NoteContent.ToChecklist(content);
        Assert.Empty(NoteContent.ParseMarks(checklist));
        Assert.Equal(new[] { "bir", "iki" }, NoteContent.ParseItems(checklist).Select(i => i.Text));
    }

    [Fact]
    public void TurkishAndSurrogatePairs_UseUtf16Offsets()
    {
        var text = "İstanbul 😀 kalın ğüşıöç";
        var start = text.IndexOf("kalın", StringComparison.Ordinal);
        var end = start + "kalın".Length;
        var content = NoteContent.CreateText(text, new[] { M("b", start, end) });

        var marks = NoteContent.ParseMarks(content);
        Assert.Equal(new[] { M("b", start, end) }, marks);
        Assert.Equal("kalın", text.Substring(marks[0].Start, marks[0].End - marks[0].Start));
        Assert.Equal(text, NoteContent.ParseText(content));
    }
}

public sealed class RichTextRepositoryTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "sn_rt_" + Guid.NewGuid().ToString("N"));
    private readonly string _db;
    private DateTime _t = new(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);

    public RichTextRepositoryTests() => _db = Path.Combine(_dir, "notes.db");

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, true); } catch { }
    }

    private NoteRepository Repo() => new(_db, () => _t = _t.AddMinutes(1));

    [Fact]
    public void FormattedNote_SurvivesReopen_AndIsFoundByPlainText()
    {
        var text = "Toplantı yarın saat 10";
        var content = NoteContent.CreateText(text, new[] { new Mark("b", 0, 8), new Mark("u", 15, 22) });

        var id = Repo().Create(NoteType.Text, "yellow", content).Id;

        var reopened = Repo();
        var note = reopened.Get(id)!;
        Assert.Equal(content, note.Content);
        Assert.Equal(new[] { new Mark("b", 0, 8), new Mark("u", 15, 22) }, NoteContent.ParseMarks(note.Content));
        Assert.Single(reopened.ListActive("yarın"));
    }
}
