using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Windows.UI.Text;
using MdBlock = Markdig.Syntax.Block;
using MdInline = Markdig.Syntax.Inlines.Inline;
using XamlInline = Microsoft.UI.Xaml.Documents.Inline;

namespace InControl.App.Controls;

/// <summary>
/// Draws a model's markdown into a RichTextBlock: emphasis, inline code, code blocks,
/// headings, lists, quotes, tables and links. Raw HTML is shown as text, never run.
/// </summary>
public static class MarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .UseEmphasisExtras()
        .UseAutoLinks()
        .Build();

    private static readonly FontFamily Mono = new("Cascadia Mono, Consolas");

    /// <summary>
    /// Replaces the target's content with the rendered markdown.
    /// </summary>
    public static void Render(RichTextBlock target, string? markdown)
    {
        target.Blocks.Clear();
        if (string.IsNullOrEmpty(markdown))
            return;

        MarkdownDocument document;
        try
        {
            document = Markdown.Parse(markdown, Pipeline);
        }
        catch (Exception)
        {
            // A parser failure must never hide the reply. Show it as written.
            target.Blocks.Add(PlainParagraph(markdown));
            return;
        }

        foreach (var block in document)
            AddBlock(target.Blocks, block, indent: 0);

        if (target.Blocks.Count == 0)
            target.Blocks.Add(PlainParagraph(markdown));
    }

    private static Paragraph PlainParagraph(string text)
    {
        var paragraph = new Paragraph();
        paragraph.Inlines.Add(new Run { Text = text });
        return paragraph;
    }

    private static void AddBlock(BlockCollection blocks, MdBlock block, double indent)
    {
        switch (block)
        {
            case HeadingBlock heading:
            {
                var paragraph = new Paragraph
                {
                    Margin = new Thickness(indent, 8, 0, 2),
                    FontWeight = FontWeights.SemiBold,
                    FontSize = heading.Level switch { 1 => 20, 2 => 18, 3 => 16, _ => 14 }
                };
                AddInlines(paragraph.Inlines, heading.Inline);
                blocks.Add(paragraph);
                break;
            }

            case ParagraphBlock para:
            {
                var paragraph = new Paragraph { Margin = new Thickness(indent, 0, 0, 8) };
                AddInlines(paragraph.Inlines, para.Inline);
                blocks.Add(paragraph);
                break;
            }

            case FencedCodeBlock or CodeBlock:
            {
                var code = (LeafBlock)block;
                var text = string.Join("\n", code.Lines.Lines.Take(code.Lines.Count).Select(l => l.ToString())).TrimEnd();
                blocks.Add(CodeParagraph(text, indent));
                break;
            }

            case ListBlock list:
            {
                var number = 1;
                if (list.IsOrdered && int.TryParse(list.OrderedStart, out var start))
                    number = start;

                foreach (var item in list.OfType<ListItemBlock>())
                {
                    var marker = list.IsOrdered ? $"{number++}." : "•";
                    var first = true;
                    foreach (var child in item)
                    {
                        if (first && child is ParagraphBlock itemPara)
                        {
                            var paragraph = new Paragraph { Margin = new Thickness(indent + 16, 0, 0, 4), TextIndent = -16 };
                            paragraph.Inlines.Add(new Run { Text = marker + " " });
                            AddInlines(paragraph.Inlines, itemPara.Inline);
                            blocks.Add(paragraph);
                        }
                        else
                        {
                            AddBlock(blocks, child, indent + 16);
                        }

                        first = false;
                    }
                }

                break;
            }

            case QuoteBlock quote:
                foreach (var child in quote)
                {
                    var before = blocks.Count;
                    AddBlock(blocks, child, indent + 12);
                    for (var i = before; i < blocks.Count; i++)
                        blocks[i].FontStyle = FontStyle.Italic;
                }

                break;

            case ThematicBreakBlock:
                blocks.Add(new Paragraph { Margin = new Thickness(indent, 4, 0, 8), Inlines = { new Run { Text = "────────" } } });
                break;

            case Table table:
            {
                // Tables read best as aligned monospace rows inside a chat card.
                var rows = table.OfType<TableRow>()
                    .Select(r => r.OfType<TableCell>().Select(c => CellText(c)).ToList())
                    .ToList();
                var widths = new int[rows.Max(r => r.Count)];
                foreach (var row in rows)
                    for (var i = 0; i < row.Count; i++)
                        widths[i] = Math.Max(widths[i], row[i].Length);

                var lines = rows.Select(r => string.Join("  ", r.Select((c, i) => c.PadRight(widths[i])))).ToList();
                if (lines.Count > 1)
                    lines.Insert(1, string.Join("  ", widths.Select(w => new string('─', w))));

                blocks.Add(CodeParagraph(string.Join("\n", lines), indent));
                break;
            }

            case HtmlBlock html:
                blocks.Add(PlainParagraph(string.Join("\n", html.Lines.Lines.Take(html.Lines.Count).Select(l => l.ToString()))));
                break;

            case ContainerBlock container:
                foreach (var child in container)
                    AddBlock(blocks, child, indent);
                break;
        }
    }

    private static Paragraph CodeParagraph(string text, double indent)
    {
        var box = new Border
        {
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10, 8, 10, 8),
            Child = new TextBlock
            {
                Text = text,
                FontFamily = Mono,
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                IsTextSelectionEnabled = true
            }
        };

        var paragraph = new Paragraph { Margin = new Thickness(indent, 2, 0, 8) };
        paragraph.Inlines.Add(new InlineUIContainer { Child = box });
        return paragraph;
    }

    private static string CellText(TableCell cell)
    {
        var parts = new List<string>();
        foreach (var block in cell)
        {
            if (block is ParagraphBlock p && p.Inline is not null)
                parts.Add(InlineText(p.Inline));
        }

        return string.Join(" ", parts);
    }

    private static string InlineText(ContainerInline container)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var inline in container)
        {
            switch (inline)
            {
                case LiteralInline literal: sb.Append(literal.Content.ToString()); break;
                case CodeInline code: sb.Append(code.Content); break;
                case ContainerInline nested: sb.Append(InlineText(nested)); break;
                case LineBreakInline: sb.Append(' '); break;
            }
        }

        return sb.ToString();
    }

    private static void AddInlines(InlineCollection target, ContainerInline? container)
    {
        if (container is null)
            return;

        foreach (var inline in container)
            AddInline(target, inline);
    }

    private static void AddInline(InlineCollection target, MdInline inline)
    {
        switch (inline)
        {
            case LiteralInline literal:
                target.Add(new Run { Text = literal.Content.ToString() });
                break;

            case CodeInline code:
                target.Add(new Run { Text = code.Content, FontFamily = Mono });
                break;

            case LineBreakInline:
                // Chat models use single newlines as line breaks, so keep them.
                target.Add(new LineBreak());
                break;

            case EmphasisInline emphasis:
            {
                Span span = emphasis.DelimiterChar == '~' && emphasis.DelimiterCount == 2
                    ? new Span { TextDecorations = TextDecorations.Strikethrough }
                    : emphasis.DelimiterCount >= 2 ? new Bold() : new Italic();
                AddInlines(span.Inlines, emphasis);
                target.Add(span);
                break;
            }

            case AutolinkInline autolink:
                target.Add(Link(autolink.Url, autolink.Url));
                break;

            case LinkInline link when link.IsImage:
                target.Add(new Run { Text = $"[image: {InlineText(link)}]" });
                break;

            case LinkInline link:
            {
                var url = link.GetDynamicUrl?.Invoke() ?? link.Url;
                var label = InlineText(link);
                target.Add(Link(url, string.IsNullOrEmpty(label) ? url ?? string.Empty : label));
                break;
            }

            case HtmlInline html:
                target.Add(new Run { Text = html.Tag });
                break;

            case HtmlEntityInline entity:
                target.Add(new Run { Text = entity.Transcoded.ToString() });
                break;

            case ContainerInline container:
                AddInlines(target, container);
                break;
        }
    }

    private static XamlInline Link(string? url, string text)
    {
        // Only web links become clickable. Anything else stays plain text.
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
        {
            var link = new Hyperlink { NavigateUri = uri };
            link.Inlines.Add(new Run { Text = text });
            return link;
        }

        return new Run { Text = text };
    }
}
