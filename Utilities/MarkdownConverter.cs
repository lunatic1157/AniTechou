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
                    // Image embedded in resizable host — find Image by traversing children
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

        /// <summary>
        /// Convert Markdown text to WPF FlowDocument via Markdig HTML.
        /// </summary>
        public static FlowDocument MarkdownToFlowDocument(string markdown)
        {
            if (string.IsNullOrWhiteSpace(markdown))
                return new FlowDocument();

            try
            {
                // 1. Markdig: Markdown → HTML (full spec support)
                var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
                string html = Markdig.Markdown.ToHtml(markdown, pipeline);

                // 2. HTML → FlowDocument (via RichTextBox clipboard trick)
                return ConvertHtmlToFlowDocument(html);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MarkdownConverter] 转换失败: {ex.Message}");
                return CreateFallbackDocument(markdown);
            }
        }

        private static FlowDocument ConvertHtmlToFlowDocument(string html)
        {
            string wrapped = "<html><head><meta charset=\"utf-8\"/><style>body{font-family:'Microsoft YaHei';font-size:14px;}</style></head><body>"
                + html + "</body></html>";

            // Save clipboard
            IDataObject saved = null;
            try { saved = Clipboard.GetDataObject(); } catch { }

            try
            {
                Clipboard.SetText(wrapped, TextDataFormat.Html);

                var rtb = new System.Windows.Controls.RichTextBox();
                rtb.Paste();

                var doc = rtb.Document;

                // Detach from RichTextBox so Document can live independently
                rtb.Document = new FlowDocument();

                doc.FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei");
                doc.FontSize = 14;
                doc.PagePadding = new Thickness(0);

                return doc;
            }
            finally
            {
                // Restore original clipboard content
                try
                {
                    if (saved != null)
                        Clipboard.SetDataObject(saved);
                }
                catch { }
            }
        }

        private static FlowDocument CreateFallbackDocument(string text)
        {
            var doc = new FlowDocument
            {
                FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei"),
                FontSize = 14
            };
            doc.Blocks.Add(new Paragraph(new Run(text)));
            return doc;
        }

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
