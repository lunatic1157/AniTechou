using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Markup;
using Markdig;

namespace AniTechou.Utilities
{
    public static class MarkdownConverter
    {
        /// <summary>
        /// Convert XAML FlowDocument string to Markdown text.
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
                // If XAML parsing fails, return as plain text
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
            // Check if it's a heading by font size
            double fontSize = para.FontSize;
            string prefix = "";
            if (fontSize >= 26) prefix = "# ";
            else if (fontSize >= 22) prefix = "## ";
            else if (fontSize >= 18) prefix = "### ";

            sb.Append(prefix);
            ConvertInlinesToMarkdown(para.Inlines, sb);
            sb.AppendLine();
            sb.AppendLine(); // blank line after paragraph
        }

        private static void ConvertInlinesToMarkdown(InlineCollection inlines, StringBuilder sb)
        {
            foreach (var inline in inlines)
            {
                if (inline is Run run)
                {
                    string text = run.Text ?? "";
                    if (run.FontWeight == FontWeights.Bold)
                        sb.Append($"**{text}**");
                    else if (run.FontStyle == FontStyles.Italic)
                        sb.Append($"*{text}*");
                    else
                        sb.Append(text);
                }
                else if (inline is Bold bold)
                {
                    sb.Append("**");
                    ConvertInlinesToMarkdown(bold.Inlines, sb);
                    sb.Append("**");
                }
                else if (inline is Italic italic)
                {
                    sb.Append("*");
                    ConvertInlinesToMarkdown(italic.Inlines, sb);
                    sb.Append("*");
                }
                else if (inline is Underline underline)
                {
                    sb.Append("<u>");
                    ConvertInlinesToMarkdown(underline.Inlines, sb);
                    sb.Append("</u>");
                }
                else if (inline is Hyperlink link)
                {
                    string url = link.NavigateUri?.ToString() ?? "";
                    sb.Append("[");
                    ConvertInlinesToMarkdown(link.Inlines, sb);
                    sb.Append($"]({url})");
                }
                else if (inline is InlineUIContainer container && container.Child is System.Windows.Controls.Grid grid)
                {
                    // Image embedded in resizable host
                    var img = grid.FindName("ContentImage") as System.Windows.Controls.Image;
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

        /// <summary>
        /// Convert Markdown text to WPF FlowDocument.
        /// </summary>
        public static FlowDocument MarkdownToFlowDocument(string markdown)
        {
            if (string.IsNullOrWhiteSpace(markdown))
                return new FlowDocument();

            try
            {
                var doc = new FlowDocument
                {
                    FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei"),
                    FontSize = 14,
                    PagePadding = new Thickness(0)
                };

                var lines = markdown.Replace("\r\n", "\n").Split('\n');
                Paragraph currentPara = null;

                foreach (var line in lines)
                {
                    string trimmed = line.TrimEnd();

                    if (string.IsNullOrWhiteSpace(trimmed))
                    {
                        currentPara = null;
                        continue;
                    }

                    // Headings
                    if (trimmed.StartsWith("### "))
                    {
                        var para = new Paragraph(ParseInlineMarkdown(trimmed.Substring(4))) { FontSize = 18, FontWeight = FontWeights.Bold };
                        doc.Blocks.Add(para);
                        currentPara = null;
                    }
                    else if (trimmed.StartsWith("## "))
                    {
                        var para = new Paragraph(ParseInlineMarkdown(trimmed.Substring(3))) { FontSize = 22, FontWeight = FontWeights.Bold };
                        doc.Blocks.Add(para);
                        currentPara = null;
                    }
                    else if (trimmed.StartsWith("# "))
                    {
                        var para = new Paragraph(ParseInlineMarkdown(trimmed.Substring(2))) { FontSize = 26, FontWeight = FontWeights.Bold };
                        doc.Blocks.Add(para);
                        currentPara = null;
                    }
                    // Unordered list
                    else if (trimmed.StartsWith("- ") || trimmed.StartsWith("* "))
                    {
                        string text = trimmed.Substring(2);
                        var listItem = new ListItem(new Paragraph(ParseInlineMarkdown(text)));
                        if (doc.Blocks.LastBlock is List lastList && lastList.MarkerStyle == TextMarkerStyle.Disc)
                            lastList.ListItems.Add(listItem);
                        else
                        {
                            var list = new List { MarkerStyle = TextMarkerStyle.Disc };
                            list.ListItems.Add(listItem);
                            doc.Blocks.Add(list);
                        }
                        currentPara = null;
                    }
                    // Ordered list
                    else if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^\d+\.\s"))
                    {
                        string text = System.Text.RegularExpressions.Regex.Replace(trimmed, @"^\d+\.\s", "");
                        var listItem = new ListItem(new Paragraph(ParseInlineMarkdown(text)));
                        if (doc.Blocks.LastBlock is List lastList && lastList.MarkerStyle == TextMarkerStyle.Decimal)
                            lastList.ListItems.Add(listItem);
                        else
                        {
                            var list = new List { MarkerStyle = TextMarkerStyle.Decimal };
                            list.ListItems.Add(listItem);
                            doc.Blocks.Add(list);
                        }
                        currentPara = null;
                    }
                    else
                    {
                        // Regular paragraph or continuation
                        if (currentPara == null)
                        {
                            currentPara = new Paragraph();
                            doc.Blocks.Add(currentPara);
                        }
                        else
                        {
                            currentPara.Inlines.Add(new LineBreak());
                        }
                        // Parse inline markdown for regular paragraphs too
                        currentPara.Inlines.Add(ParseInlineMarkdown(trimmed));
                    }
                }

                return doc;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MarkdownConverter] 转换失败: {ex.Message}");
                return new FlowDocument(new Paragraph(new Run(markdown)));
            }
        }

        private static Inline ParseInlineMarkdown(string text)
        {
            var span = new Span();
            if (string.IsNullOrEmpty(text))
                return span;

            int i = 0;
            int plainStart = 0;

            void FlushPlainText(int end)
            {
                if (plainStart < end)
                {
                    span.Inlines.Add(new Run(text.Substring(plainStart, end - plainStart)));
                    plainStart = end;
                }
            }

            while (i < text.Length)
            {
                // Bold **...**
                if (i < text.Length - 3 && text[i] == '*' && text[i + 1] == '*')
                {
                    int end = text.IndexOf("**", i + 2);
                    if (end > i)
                    {
                        FlushPlainText(i);
                        span.Inlines.Add(new Bold(new Run(text.Substring(i + 2, end - i - 2))));
                        i = end + 2;
                        plainStart = i;
                        continue;
                    }
                }
                // Italic *...* (single *, not part of **)
                if (i < text.Length - 1 && text[i] == '*' && text[i + 1] != '*')
                {
                    int end = text.IndexOf('*', i + 1);
                    if (end > i)
                    {
                        FlushPlainText(i);
                        span.Inlines.Add(new Italic(new Run(text.Substring(i + 1, end - i - 1))));
                        i = end + 1;
                        plainStart = i;
                        continue;
                    }
                }
                // Link [text](url)
                if (text[i] == '[')
                {
                    int closeB = text.IndexOf(']', i);
                    if (closeB > i && closeB + 1 < text.Length && text[closeB + 1] == '(')
                    {
                        int closeP = text.IndexOf(')', closeB + 2);
                        if (closeP > closeB)
                        {
                            FlushPlainText(i);
                            string linkText = text.Substring(i + 1, closeB - i - 1);
                            string url = text.Substring(closeB + 2, closeP - closeB - 2);
                            try
                            {
                                span.Inlines.Add(new Hyperlink(new Run(linkText)) { NavigateUri = new Uri(url) });
                            }
                            catch
                            {
                                span.Inlines.Add(new Run(linkText));
                            }
                            i = closeP + 1;
                            plainStart = i;
                            continue;
                        }
                    }
                }
                i++;
            }

            FlushPlainText(text.Length);
            return span;
        }
    }
}
