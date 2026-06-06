using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using Markdig;
using Markdig.Syntax.Inlines;
using MDBlock = Markdig.Syntax.Block;
using MDCodeBlock = Markdig.Syntax.CodeBlock;
using MDHeadingBlock = Markdig.Syntax.HeadingBlock;
using MDParagraphBlock = Markdig.Syntax.ParagraphBlock;
using MDFencedCodeBlock = Markdig.Syntax.FencedCodeBlock;
using MDListBlock = Markdig.Syntax.ListBlock;
using MDListItemBlock = Markdig.Syntax.ListItemBlock;
using MDThematicBreakBlock = Markdig.Syntax.ThematicBreakBlock;
using MDQuoteBlock = Markdig.Syntax.QuoteBlock;
using MDTable = Markdig.Extensions.Tables.Table;
using MDTableRow = Markdig.Extensions.Tables.TableRow;
using MDTableCell = Markdig.Extensions.Tables.TableCell;
using MDLeafBlock = Markdig.Syntax.LeafBlock;
using MDContainerBlock = Markdig.Syntax.ContainerBlock;
using MDInline = Markdig.Syntax.Inlines.Inline;
using WpfInline = System.Windows.Documents.Inline;

namespace AniTechou.Utilities
{
    public static class MarkdownConverter
    {
        /// <summary>
        /// [Migration-only] Convert XAML FlowDocument string to Markdown text.
        /// Only used for one-time migration of old XAML-format notes to Markdown.
        /// Not called during normal editor operation.
        /// </summary>
        public static string XamlToMarkdown(string xamlContent)
        {
            if (string.IsNullOrWhiteSpace(xamlContent))
                return "";

            try
            {
                FlowDocument doc;
                if (xamlContent.TrimStart().StartsWith("<FlowDocument"))
                {
                    using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xamlContent));
                    doc = (FlowDocument)XamlReader.Load(stream);
                }
                else
                {
                    // Plain text fallback
                    return xamlContent;
                }

                var sb = new StringBuilder();
                ConvertBlocksToMarkdown(doc.Blocks, sb);
                return sb.ToString().TrimEnd();
            }
            catch
            {
                return xamlContent;
            }
        }

        private static void ConvertBlocksToMarkdown(BlockCollection blocks, StringBuilder sb)
        {
            foreach (var block in blocks)
            {
                if (block is Paragraph para)
                    ConvertParagraphToMarkdown(para, sb);
                else if (block is List list)
                    ConvertListToMarkdown(list, sb);
                else if (block is Section section)
                    ConvertBlocksToMarkdown(section.Blocks, sb);
                else if (block is Table table)
                    sb.AppendLine("*(表格暂不支持转换)*");
            }
        }

        private static void ConvertParagraphToMarkdown(Paragraph para, StringBuilder sb)
        {
            double fontSize = para.FontSize;
            string prefix = "";
            if (fontSize >= 26) prefix = "# ";
            else if (fontSize >= 22) prefix = "## ";
            else if (fontSize >= 18) prefix = "### ";

            sb.Append(prefix);
            ConvertInlinesToMarkdown(para.Inlines, sb);
            sb.AppendLine();
            sb.AppendLine();
        }

        private static void ConvertInlinesToMarkdown(InlineCollection inlines, StringBuilder sb,
            bool inBold = false, bool inItalic = false)
        {
            foreach (var inline in inlines)
            {
                if (inline is Run run)
                {
                    string text = run.Text ?? "";
                    // Only apply **/* markers for direct formatting on Run when NOT
                    // already inside Bold/Italic (which add their own markers).
                    // Otherwise inherited font properties cause double-wrapping:
                    // Bold->Run -> "**" + Run(says Bold)="**text**" + "**" = ****text****
                    if (!inBold && run.FontWeight == FontWeights.Bold)
                        sb.Append($"**{text}**");
                    else if (!inItalic && run.FontStyle == FontStyles.Italic)
                        sb.Append($"*{text}*");
                    else
                        sb.Append(text);
                }
                else if (inline is Bold bold)
                {
                    sb.Append("**");
                    ConvertInlinesToMarkdown(bold.Inlines, sb, inBold: true, inItalic);
                    sb.Append("**");
                }
                else if (inline is Italic italic)
                {
                    sb.Append("*");
                    ConvertInlinesToMarkdown(italic.Inlines, sb, inBold, inItalic: true);
                    sb.Append("*");
                }
                else if (inline is Underline underline)
                {
                    sb.Append("<u>");
                    ConvertInlinesToMarkdown(underline.Inlines, sb, inBold, inItalic);
                    sb.Append("</u>");
                }
                else if (inline is Hyperlink link)
                {
                    string url = link.NavigateUri?.ToString() ?? "";
                    sb.Append("[");
                    ConvertInlinesToMarkdown(link.Inlines, sb, inBold, inItalic);
                    sb.Append($"]({url})");
                }
                else if (inline is Span span)
                {
                    // Span can carry font properties but should not double-wrap
                    ConvertInlinesToMarkdown(span.Inlines, sb, inBold, inItalic);
                }
                else if (inline is InlineUIContainer container && container.Child is System.Windows.Controls.Grid grid)
                {
                    var img = FindImageInGrid(grid);
                    if (img != null)
                    {
                        string imgPath = img.Uid ?? "";
                        if (imgPath.StartsWith("ani-image:"))
                            imgPath = imgPath.Substring("ani-image:".Length);
                        sb.Append($"![]({imgPath})");
                    }
                }
                else if (inline is LineBreak)
                {
                    sb.AppendLine();
                }
            }
        }

        private static void ConvertListToMarkdown(List list, StringBuilder sb)
        {
            bool ordered = list.MarkerStyle != TextMarkerStyle.Disc &&
                           list.MarkerStyle != TextMarkerStyle.Circle &&
                           list.MarkerStyle != TextMarkerStyle.Box;

            int index = 1;
            foreach (var item in list.ListItems)
            {
                string prefix = ordered ? $"{index}. " : "- ";
                index++;
                sb.Append(prefix);
                ConvertBlocksToMarkdown(item.Blocks, sb);
            }
            sb.AppendLine();
        }

        // =========================================================================
        // Markdown → FlowDocument (Markdig AST walker — no HTML, no clipboard)
        // =========================================================================

        private static readonly FontFamily DefaultFont = new FontFamily("Microsoft YaHei");
        private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        private const double DefaultFontSize = 14.0;

        /// <summary>
        /// Convert Markdown text to WPF FlowDocument by walking Markdig AST.
        /// </summary>
        public static FlowDocument MarkdownToFlowDocument(string markdown)
        {
            var doc = new FlowDocument
            {
                FontFamily = DefaultFont,
                FontSize = DefaultFontSize,
                PagePadding = new Thickness(0)
            };

            if (string.IsNullOrWhiteSpace(markdown))
                return doc;

            try
            {
                var mdDoc = Markdig.Markdown.Parse(markdown, Pipeline);

                foreach (var block in mdDoc)
                    ConvertBlock(block, doc.Blocks);

                return doc;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MarkdownConverter] 转换失败: {ex.Message}");
                doc.Blocks.Add(new Paragraph(new Run(markdown)));
                return doc;
            }
        }

        private static void ConvertBlock(MDBlock block, BlockCollection output)
        {
            switch (block)
            {
                case MDHeadingBlock heading:
                    output.Add(ConvertHeading(heading));
                    break;
                case MDParagraphBlock para:
                    output.Add(ConvertParagraph(para));
                    break;
                case MDFencedCodeBlock fenced:
                    output.Add(ConvertFencedCodeBlock(fenced));
                    break;
                case MDCodeBlock code:
                    output.Add(ConvertCodeBlock(code));
                    break;
                case MDListBlock listBlock:
                    var list = ConvertList(listBlock);
                    if (list != null)
                        output.Add(list);
                    break;
                case MDThematicBreakBlock _:
                    output.Add(ConvertThematicBreak());
                    break;
                case MDQuoteBlock quote:
                    var section = ConvertQuote(quote);
                    if (section != null)
                        output.Add(section);
                    break;
                case MDTable table:
                    var wpfTable = ConvertTable(table);
                    if (wpfTable != null)
                        output.Add(wpfTable);
                    break;
                default:
                    // Fallback for leaf blocks with inline content
                    if (block is MDLeafBlock leaf && leaf.Inline != null)
                    {
                        var fallback = new Paragraph();
                        var fallbackState = new HtmlState();
                        foreach (var inline in leaf.Inline)
                            fallback.Inlines.Add(ConvertInline(inline, fallbackState));
                        if (fallback.Inlines.Count > 0)
                            output.Add(fallback);
                    }
                    break;
            }
        }

        // --- Block converters ---

        private static Paragraph ConvertHeading(MDHeadingBlock heading)
        {
            double fontSize = heading.Level switch
            {
                1 => 26,
                2 => 22,
                3 => 18,
                4 => 16,
                _ => 15
            };
            var para = new Paragraph
            {
                FontSize = fontSize,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, fontSize * 0.4, 0, 4)
            };
            var headingState = new HtmlState();
            foreach (var inline in heading.Inline)
                para.Inlines.Add(ConvertInline(inline, headingState));
            return para;
        }

        private static Paragraph ConvertParagraph(MDParagraphBlock paraBlock)
        {
            var para = new Paragraph
            {
                Margin = new Thickness(0, 0, 0, 4)
            };
            if (paraBlock.Inline == null)
                return para;

            var paraState = new HtmlState();
            foreach (var inline in paraBlock.Inline)
                para.Inlines.Add(ConvertInline(inline, paraState));
            return para;
        }

        private static Paragraph ConvertCodeBlock(MDCodeBlock codeBlock)
        {
            var para = new Paragraph
            {
                FontFamily = new FontFamily("Consolas, Courier New"),
                FontSize = 13,
                Background = new SolidColorBrush(Color.FromRgb(40, 44, 52)),
                Foreground = new SolidColorBrush(Color.FromRgb(171, 178, 191)),
                Margin = new Thickness(0, 4, 0, 8),
                Padding = new Thickness(10, 8, 10, 8)
            };
            string code = codeBlock.Lines.ToString();
            para.Inlines.Add(new Run(code.TrimEnd('\r', '\n')));
            return para;
        }

        private static Paragraph ConvertFencedCodeBlock(MDFencedCodeBlock fenced)
        {
            var para = new Paragraph
            {
                FontFamily = new FontFamily("Consolas, Courier New"),
                FontSize = 13,
                Background = new SolidColorBrush(Color.FromRgb(40, 44, 52)),
                Foreground = new SolidColorBrush(Color.FromRgb(171, 178, 191)),
                Margin = new Thickness(0, 4, 0, 8),
                Padding = new Thickness(10, 8, 10, 8)
            };
            string code = fenced.Lines.ToString();
            para.Inlines.Add(new Run(code.TrimEnd('\r', '\n')));
            return para;
        }

        private static System.Windows.Documents.List ConvertList(MDListBlock listBlock)
        {
            var list = new System.Windows.Documents.List
            {
                Margin = new Thickness(20, 2, 0, 6)
            };

            // Check if ordered by looking at the bullet type
            if (listBlock.IsOrdered)
                list.MarkerStyle = TextMarkerStyle.Decimal;
            else
                list.MarkerStyle = TextMarkerStyle.Disc;

            foreach (var item in listBlock)
            {
                if (item is MDListItemBlock listItem)
                {
                    var listItemWpf = new ListItem();
                    // Each ListItem can contain multiple blocks (paragraphs, nested lists, etc.)
                    foreach (var child in listItem)
                        ConvertBlock(child, listItemWpf.Blocks);

                    if (listItemWpf.Blocks.Count == 0)
                        listItemWpf.Blocks.Add(new Paragraph());
                    list.ListItems.Add(listItemWpf);
                }
            }

            return list;
        }

        private static Paragraph ConvertThematicBreak()
        {
            var para = new Paragraph
            {
                Margin = new Thickness(0, 8, 0, 8),
                BorderThickness = new Thickness(0, 0, 0, 1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(128, 128, 128)),
                Padding = new Thickness(0, 0, 0, 4)
            };
            return para;
        }

        private static Section ConvertQuote(MDQuoteBlock quote)
        {
            var section = new Section
            {
                BorderThickness = new Thickness(4, 0, 0, 0),
                BorderBrush = new SolidColorBrush(Color.FromRgb(100, 149, 237)),
                Padding = new Thickness(12, 4, 0, 4),
                Margin = new Thickness(0, 4, 0, 8)
            };
            foreach (var child in quote)
                ConvertBlock(child, section.Blocks);
            return section;
        }

        private static Table ConvertTable(MDTable table)
        {
            var wpfTable = new Table
            {
                CellSpacing = 0,
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                Margin = new Thickness(0, 4, 0, 8)
            };

            // Define columns from first row
            MDTableRow firstMdRow = null;
            foreach (var r in table)
            {
                if (r is MDTableRow tr) { firstMdRow = tr; break; }
            }

            if (firstMdRow == null)
                return null;

            int colCount = 0;
            foreach (var _ in firstMdRow) colCount++;

            if (colCount == 0)
                return null;

            for (int c = 0; c < colCount; c++)
                wpfTable.Columns.Add(new TableColumn { Width = GridLength.Auto });

            bool firstRow = true;
            var rowGroup = new TableRowGroup();
            foreach (var mdRowObj in table)
            {
                if (mdRowObj is MDTableRow mdRow)
                {
                    var wpfRow = new TableRow();
                    if (firstRow)
                        wpfRow.Background = new SolidColorBrush(Color.FromRgb(55, 55, 60));

                    foreach (var mdCellObj in mdRow)
                    {
                        if (mdCellObj is MDTableCell mdCell)
                        {
                            var wpfCell = new TableCell
                            {
                                Padding = new Thickness(8, 4, 8, 4),
                                BorderThickness = new Thickness(0.5),
                                BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 65))
                            };
                            foreach (var child in mdCell)
                                ConvertBlock(child, wpfCell.Blocks);

                            if (wpfCell.Blocks.Count == 0)
                                wpfCell.Blocks.Add(new Paragraph());
                            wpfRow.Cells.Add(wpfCell);
                        }
                    }
                    rowGroup.Rows.Add(wpfRow);
                    firstRow = false;
                }
            }
            wpfTable.RowGroups.Add(rowGroup);
            return wpfTable;
        }

        // --- HTML inline state tracker ---

        /// <summary>
        /// Tracks active HTML inline formatting tags (u, span) across sibling inlines.
        /// Markdig parses &lt;u&gt;text&lt;/u&gt; as HtmlInline("&lt;u&gt;") + LiteralInline("text") + HtmlInline("&lt;/u&gt;").
        /// </summary>
        private sealed class HtmlState
        {
            public bool Underline;
            public Brush Foreground;
            public Brush Background;

            public void ProcessTag(string tag)
            {
                if (tag.Equals("<u>", StringComparison.OrdinalIgnoreCase))
                    Underline = true;
                else if (tag.Equals("</u>", StringComparison.OrdinalIgnoreCase))
                    Underline = false;
                else if (tag.StartsWith("<span ", StringComparison.OrdinalIgnoreCase) && tag.EndsWith(">"))
                    ParseSpan(tag);
                else if (tag.Equals("</span>", StringComparison.OrdinalIgnoreCase))
                {
                    Foreground = null;
                    Background = null;
                }
            }

            private void ParseSpan(string tag)
            {
                int styleIdx = tag.IndexOf("style=\"", StringComparison.OrdinalIgnoreCase);
                if (styleIdx < 0) return;
                int start = styleIdx + 7;
                int end = tag.IndexOf('"', start);
                if (end < 0) return;
                string style = tag.Substring(start, end - start);

                foreach (string part in style.Split(';'))
                {
                    var kv = part.Split(':', 2);
                    if (kv.Length != 2) continue;
                    string key = kv[0].Trim().ToLowerInvariant();
                    string val = kv[1].Trim();

                    if (key == "color" && TryParseColor(val, out var fg))
                        Foreground = new SolidColorBrush(fg);
                    else if (key == "background-color" && TryParseColor(val, out var bg))
                        Background = new SolidColorBrush(bg);
                }
            }

            private static bool TryParseColor(string val, out Color color)
            {
                color = Colors.Transparent;
                if (string.IsNullOrEmpty(val)) return false;
                try
                {
                    if (val.StartsWith("#") && val.Length == 9) // #AARRGGBB
                        val = "#" + val.Substring(3); // strip alpha for WPF
                    var c = (Color)ColorConverter.ConvertFromString(val);
                    color = c;
                    return true;
                }
                catch { return false; }
            }

        /// <summary>
        /// Returns a copy of this state (for entering nested containers).
        /// </summary>
        public HtmlState Clone()
        {
            return new HtmlState { Underline = Underline, Foreground = Foreground, Background = Background };
        }

        /// <summary>
        /// Wraps an inline with the active HTML formatting.
        /// </summary>
        public WpfInline Apply(WpfInline inner)
        {
            WpfInline result = inner;

            if (Foreground != null || Background != null)
            {
                if (result is Run run)
                {
                    if (Foreground != null) run.Foreground = Foreground;
                    if (Background != null) run.Background = Background;
                }
                else if (result is Span span)
                {
                    if (Foreground != null) span.Foreground = Foreground;
                    if (Background != null) span.Background = Background;
                }
            }

            if (Underline && !(result is Underline))
            {
                var u = new Underline();
                u.Inlines.Add(result);
                result = u;
            }

            return result;
        }
    }

        // --- Inline converter ---

        private static WpfInline ConvertInline(MDInline inline, HtmlState htmlState = null)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    var run = new Run(literal.Content.ToString());
                    return htmlState != null ? htmlState.Apply(run) : run;

                case EmphasisInline emphasis:
                    var emphasisResult = ConvertEmphasis(emphasis, htmlState);
                    return htmlState != null ? htmlState.Apply(emphasisResult) : emphasisResult;

                case LinkInline link:
                    if (link.IsImage)
                        return ConvertImage(link);
                    return ConvertHyperlink(link, htmlState);

                case CodeInline code:
                    var codeRun = new Run(code.Content)
                    {
                        FontFamily = new FontFamily("Consolas, Courier New"),
                        Background = new SolidColorBrush(Color.FromRgb(55, 55, 60)),
                        Foreground = new SolidColorBrush(Color.FromRgb(224, 108, 117))
                    };
                    return htmlState != null ? htmlState.Apply(codeRun) : codeRun;

                case LineBreakInline _:
                    return new LineBreak();

                case HtmlInline html:
                    // Update tracker state; the tag itself is invisible
                    htmlState?.ProcessTag(html.Tag);
                    return new Run(""); // invisible placeholder

                case HtmlEntityInline entity:
                    var entityRun = new Run(System.Net.WebUtility.HtmlDecode(entity.Transcoded.ToString()));
                    return htmlState != null ? htmlState.Apply(entityRun) : entityRun;

                default:
                    // Fallback for unknown inline types — walk children if container
                    if (inline is ContainerInline container)
                    {
                        var span = new Span();
                        // Do NOT pass htmlState to children — outer Apply() wraps the entire result
                        foreach (var child in container)
                            span.Inlines.Add(ConvertInline(child));
                        return htmlState != null ? htmlState.Apply(span) : span;
                    }
                    // Best-effort text representation
                    var text = inline.ToString();
                    var textRun = string.IsNullOrEmpty(text) ? new Run("") : new Run(text);
                    return htmlState != null ? htmlState.Apply(textRun) : textRun;
            }
        }

        private static WpfInline ConvertEmphasis(EmphasisInline emphasis, HtmlState htmlState = null)
        {
            int count = emphasis.DelimiterCount;

            // Build inner content once, then snapshot — WPF Inline can only have
            // one parent, so adding to Italic/Bold removes from innerSpan.Inlines.
            // Note: do NOT pass htmlState to children — the outer Apply() wraps
            // the entire result, avoiding double-wrapping.
            var innerSpan = new Span();
            foreach (var child in emphasis)
                innerSpan.Inlines.Add(ConvertInline(child));
            var children = innerSpan.Inlines.ToList();

            // Delimiter count: 1=italic, 2=bold, 3=bold+italic
            if (count == 1)
            {
                var italic = new Italic();
                foreach (WpfInline child in children)
                    italic.Inlines.Add(child);
                return italic;
            }
            if (count == 2)
            {
                var bold = new Bold();
                foreach (WpfInline child in children)
                    bold.Inlines.Add(child);
                return bold;
            }
            // count >= 3: bold + italic
            var boldItalic = new Bold();
            var italicInner = new Italic();
            foreach (WpfInline child in children)
                italicInner.Inlines.Add(child);
            boldItalic.Inlines.Add(italicInner);
            return boldItalic;
        }

        private static WpfInline ConvertHyperlink(LinkInline link, HtmlState htmlState = null)
        {
            string url = link.Url ?? link.GetDynamicUrl?.Invoke() ?? "";
            var hyperlink = new Hyperlink
            {
                NavigateUri = string.IsNullOrEmpty(url) ? null : new Uri(url),
                Foreground = new SolidColorBrush(Color.FromRgb(100, 149, 237)),
                TextDecorations = TextDecorations.Underline
            };
            // Do NOT pass htmlState to children — outer Apply() wraps the entire result
            foreach (var child in link)
                hyperlink.Inlines.Add(ConvertInline(child));
            return hyperlink;
        }

        private static WpfInline ConvertImage(LinkInline link)
        {
            string url = link.Url ?? link.GetDynamicUrl?.Invoke() ?? "";
            string alt = "";
            if (link.FirstChild is LiteralInline lit)
                alt = lit.Content.ToString();

            // Try to load and display the actual image
            if (!string.IsNullOrWhiteSpace(url) && File.Exists(url))
            {
                try
                {
                    var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(url, UriKind.Absolute);
                    bitmap.EndInit();
                    bitmap.Freeze();

                    var image = new System.Windows.Controls.Image
                    {
                        Source = bitmap,
                        Stretch = System.Windows.Media.Stretch.Uniform,
                        MaxWidth = 700,
                        MaxHeight = 500,
                        Margin = new Thickness(0, 6, 0, 6)
                    };
                    if (!string.IsNullOrWhiteSpace(alt))
                        image.ToolTip = alt;

                    return new InlineUIContainer(image);
                }
                catch
                {
                    // Fall through to placeholder
                }
            }

            // Fallback placeholder when image file not available
            var span = new Span { Foreground = new SolidColorBrush(Colors.Gray) };
            span.Inlines.Add(new Run($"🖼 {(string.IsNullOrEmpty(alt) ? "图片" : alt)}"));
            if (!string.IsNullOrEmpty(url))
            {
                span.Inlines.Add(new LineBreak());
                span.Inlines.Add(new Run(url) { FontSize = 11 });
            }
            return span;
        }

        // --- Helpers ---

        private static System.Windows.Controls.Image FindImageInGrid(System.Windows.Controls.Grid grid)
        {
            foreach (var child in grid.Children)
            {
                if (child is System.Windows.Controls.Border border && border.Child is System.Windows.Controls.Image img)
                    return img;
                if (child is System.Windows.Controls.Image directImg)
                    return directImg;
            }
            return null;
        }
    }
}
