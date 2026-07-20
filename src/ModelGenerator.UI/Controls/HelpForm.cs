using System.IO;
using Markdig;

namespace ModelGenerator.UI.Controls;

/// <summary>Renders docs/HOW_TO_USE.md (copied alongside the exe at build time, see the
/// Help\ ItemGroup in ModelGenerator.UI.csproj) as HTML in an embedded WebBrowser control.</summary>
public class HelpForm : Form
{
    public HelpForm()
    {
        Text = "How to Use — 3D Model Generator";
        Width = 900;
        Height = 800;
        StartPosition = FormStartPosition.CenterParent;

        var browser = new WebBrowser { Dock = DockStyle.Fill };
        Controls.Add(browser);

        string helpDir = Path.Combine(AppContext.BaseDirectory, "Help");
        string markdownPath = Path.Combine(helpDir, "HOW_TO_USE.md");
        string htmlPath = Path.Combine(helpDir, "HOW_TO_USE.html");

        if (File.Exists(markdownPath))
        {
            string markdown = File.ReadAllText(markdownPath);
            string body = Markdown.ToHtml(markdown, new MarkdownPipelineBuilder().UseAdvancedExtensions().Build());
            File.WriteAllText(htmlPath, WrapInHtmlDocument(body));
            browser.Navigate(new Uri(htmlPath));
        }
        else
        {
            browser.DocumentText = "<html><body><p>Help content not found.</p></body></html>";
        }
    }

    private const string HtmlTemplate = """
        <html>
        <head>
        <meta charset="utf-8">
        <style>
            body { font-family: Segoe UI, Arial, sans-serif; font-size: 14px; line-height: 1.5;
                    color: #222; max-width: 800px; margin: 16px auto; padding: 0 16px; }
            h1, h2, h3 { color: #103; }
            h1 { border-bottom: 2px solid #ddd; padding-bottom: 6px; }
            h2 { border-bottom: 1px solid #eee; padding-bottom: 4px; margin-top: 28px; }
            img { max-width: 100%; border: 1px solid #ccc; border-radius: 4px; margin: 8px 0; }
            code { background: #f2f2f2; padding: 1px 4px; border-radius: 3px; font-family: Consolas, monospace; }
            pre { background: #f2f2f2; padding: 8px; border-radius: 4px; overflow-x: auto; }
            a { color: #0563c1; }
        </style>
        </head>
        <body>
        __BODY__
        </body>
        </html>
        """;

    private static string WrapInHtmlDocument(string bodyHtml) => HtmlTemplate.Replace("__BODY__", bodyHtml);
}
