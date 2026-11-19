using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using StickyNotes.Core;

namespace StickyNotes.App;

/// <summary>
/// FlowDocument ↔ (düz metin, biçim aralıkları) dönüşümü. Ofsetler UTF-16 kod birimi; her paragraf geçişi 1 karakter (\n).
/// Read, GetOffset ve GetPointer aynı sayım kuralını kullanır.
/// Yalnızca Paragraph blokları desteklenir: Write yalnızca paragraf üretir, liste/tablo komutları engellenmiştir.
/// </summary>
internal static class RichTextMapper
{
    private const int BoldFlag = 1, UnderlineFlag = 2, StrikeFlag = 4;

    /// <summary>Belgeyi metin + biçim aralıklarından yeniden kurar (tek paragraf = tek satır).</summary>
    public static void Write(FlowDocument doc, string text, IReadOnlyList<Mark> marks)
    {
        doc.Blocks.Clear();
        var lineStart = 0;
        foreach (var line in text.Split('\n'))
        {
            var p = new Paragraph();
            var i = 0;
            while (i < line.Length)
            {
                var flags = FlagsAt(marks, lineStart + i);
                var j = i + 1;
                while (j < line.Length && FlagsAt(marks, lineStart + j) == flags) j++;

                var run = new Run(line.Substring(i, j - i));
                if ((flags & BoldFlag) != 0) run.FontWeight = FontWeights.Bold;
                var decos = new TextDecorationCollection();
                if ((flags & UnderlineFlag) != 0) foreach (var d in TextDecorations.Underline) decos.Add(d);
                if ((flags & StrikeFlag) != 0) foreach (var d in TextDecorations.Strikethrough) decos.Add(d);
                if (decos.Count > 0) run.TextDecorations = decos;
                p.Inlines.Add(run);
                i = j;
            }
            doc.Blocks.Add(p);
            lineStart += line.Length + 1;
        }
    }

    /// <summary>Belgeyi düz metin ve (normalize edilmiş) biçim aralıklarına çevirir.</summary>
    public static (string Text, List<Mark> Marks) Read(FlowDocument doc)
    {
        var sb = new StringBuilder();
        var raw = new List<Mark>();
        var first = true;
        foreach (var p in doc.Blocks.OfType<Paragraph>())
        {
            if (!first) sb.Append('\n');
            first = false;
            foreach (var leaf in Leaves(p.Inlines))
            {
                if (leaf is Run run)
                {
                    var start = sb.Length;
                    sb.Append(run.Text);
                    var flags = FormatOf(run);
                    if (flags == 0 || run.Text.Length == 0) continue;
                    if ((flags & BoldFlag) != 0) raw.Add(new Mark(MarkTypes.Bold, start, sb.Length));
                    if ((flags & UnderlineFlag) != 0) raw.Add(new Mark(MarkTypes.Underline, start, sb.Length));
                    if ((flags & StrikeFlag) != 0) raw.Add(new Mark(MarkTypes.Strike, start, sb.Length));
                }
                else if (leaf is LineBreak) sb.Append('\n');
            }
        }
        var text = sb.ToString();
        return (text, RichText.Normalize(raw, text.Length));
    }

    /// <summary>Belge konumunun metin ofseti (Read ile aynı sayım).</summary>
    public static int GetOffset(FlowDocument doc, TextPointer pointer)
    {
        var offset = 0;
        var first = true;
        foreach (var p in doc.Blocks.OfType<Paragraph>())
        {
            if (!first) offset++;
            first = false;
            if (pointer.CompareTo(p.ContentStart) <= 0) return offset;
            if (pointer.CompareTo(p.ContentEnd) <= 0) return offset + OffsetInParagraph(p, pointer);
            offset += ParagraphLength(p);
        }
        return offset;
    }

    /// <summary>Metin ofsetinin belge konumu (GetOffset'in tersi).</summary>
    public static TextPointer GetPointer(FlowDocument doc, int offset)
    {
        var pos = 0;
        var first = true;
        Paragraph? last = null;
        foreach (var p in doc.Blocks.OfType<Paragraph>())
        {
            if (!first) pos++;
            first = false;
            var len = ParagraphLength(p);
            if (offset <= pos + len) return PointerInParagraph(p, Math.Max(0, offset - pos));
            pos += len;
            last = p;
        }
        return last?.ContentEnd ?? doc.ContentEnd;
    }

    private static int FlagsAt(IReadOnlyList<Mark> marks, int index)
    {
        var flags = 0;
        foreach (var m in marks)
        {
            if (m.Start > index || index >= m.End) continue;
            flags |= m.Type switch
            {
                MarkTypes.Bold => BoldFlag,
                MarkTypes.Underline => UnderlineFlag,
                MarkTypes.Strike => StrikeFlag,
                _ => 0
            };
        }
        return flags;
    }

    /// <summary>Run'dan paragrafa kadar zincirdeki YEREL biçim değerleri (miras alınan değerler sayılmaz).</summary>
    private static int FormatOf(Run run)
    {
        var flags = 0;
        DependencyObject? node = run;
        while (node is not null)
        {
            if (node.ReadLocalValue(TextElement.FontWeightProperty) is FontWeight w
                && w.ToOpenTypeWeight() >= FontWeights.Bold.ToOpenTypeWeight())
                flags |= BoldFlag;

            if (node.ReadLocalValue(Inline.TextDecorationsProperty) is TextDecorationCollection decos)
            {
                foreach (var d in decos)
                {
                    if (d.Location == TextDecorationLocation.Underline) flags |= UnderlineFlag;
                    else if (d.Location == TextDecorationLocation.Strikethrough) flags |= StrikeFlag;
                }
            }

            if (node is Paragraph) break;
            node = LogicalTreeHelper.GetParent(node);
        }
        return flags;
    }

    /// <summary>Paragraftaki yaprak inline'lar (Span'ler açılmış), belge sırasında.</summary>
    private static IEnumerable<Inline> Leaves(InlineCollection inlines)
    {
        foreach (var inline in inlines)
        {
            if (inline is Span span)
            {
                foreach (var leaf in Leaves(span.Inlines)) yield return leaf;
            }
            else yield return inline;
        }
    }

    private static int LeafLength(Inline leaf) => leaf switch
    {
        Run run => run.Text.Length,
        LineBreak => 1,
        _ => 0
    };

    private static int ParagraphLength(Paragraph p)
    {
        var len = 0;
        foreach (var leaf in Leaves(p.Inlines)) len += LeafLength(leaf);
        return len;
    }

    private static int OffsetInParagraph(Paragraph p, TextPointer pointer)
    {
        var offset = 0;
        foreach (var leaf in Leaves(p.Inlines))
        {
            if (leaf is Run run)
            {
                if (pointer.CompareTo(run.ContentEnd) >= 0) { offset += run.Text.Length; continue; }
                if (pointer.CompareTo(run.ContentStart) <= 0) return offset;
                return offset + run.ContentStart.GetOffsetToPosition(pointer);
            }
            offset += LeafLength(leaf);
        }
        return offset;
    }

    private static TextPointer PointerInParagraph(Paragraph p, int k)
    {
        foreach (var leaf in Leaves(p.Inlines))
        {
            if (leaf is Run run)
            {
                if (k <= run.Text.Length)
                    return run.ContentStart.GetPositionAtOffset(k) ?? run.ContentEnd;
                k -= run.Text.Length;
            }
            else k -= LeafLength(leaf);
        }
        return p.ContentEnd;
    }
}
