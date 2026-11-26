using StickyNotes.Core;

namespace StickyNotes.Core.Tests;

public class MarkdownListTests
{
    [Theory]
    [InlineData("- süt", 2, "- ", false)]
    [InlineData("* a", 2, "* ", false)]
    [InlineData("  + a", 4, "  + ", false)]
    [InlineData("9. a", 3, "10. ", false)]
    [InlineData("1) a", 3, "2) ", false)]
    [InlineData("- [x] bitti", 6, "- [ ] ", false)]
    [InlineData("- ", 2, "- ", true)]
    [InlineData("- [ ] ", 6, "- [ ] ", true)]
    public void ListLines_AreParsed(string line, int prefix, string next, bool empty)
    {
        Assert.Equal(new ListLine(prefix, next, empty), MarkdownList.Parse(line));
    }

    [Theory]
    [InlineData("metin")]
    [InlineData("-bitişik")]
    [InlineData("**kalın**")]
    [InlineData("# başlık")]
    [InlineData("")]
    public void OtherLines_AreNotLists(string line) => Assert.Null(MarkdownList.Parse(line));

    [Theory]
    [InlineData("  - a", 2)]
    [InlineData(" - a", 1)]
    [InlineData("\t- a", 1)]
    [InlineData("- a", 0)]
    public void Outdent_RemovesAtMostOneLevel(string line, int expected) => Assert.Equal(expected, MarkdownList.OutdentLength(line));
}
