using CelesteStudio.Controls;
using CelesteStudio.Util;
using Eto.Drawing;
using Eto.Forms;
using SkiaSharp;
using StudioCommunication.Util;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace CelesteStudio.Dialog;

public class ChangelogDialog : Eto.Forms.Dialog {

    private class ContentDrawable(string title, Dictionary<string, List<string>> categories) : SkiaDrawable {
        private static SKColorF TextColor => Eto.Platform.Instance.IsWpf && Settings.Instance.Theme.DarkMode
            ? new SKColorF(1.0f - SystemColors.ControlText.R, 1.0f - SystemColors.ControlText.G, 1.0f - SystemColors.ControlText.B)
            : SystemColors.ControlText.ToSkia();

        public override void Draw(SKSurface surface) {
            var canvas = surface.Canvas;

            var textColor = TextColor;

            using var titleFont = new SKFont(SKFontManager.Default.MatchTypeface(SKTypeface.Default, SKFontStyle.Bold), 28.0f);
            using var titlePaint = new SKPaint(titleFont);
            titlePaint.ColorF = textColor;
            titlePaint.IsAntialias = true;
            titlePaint.TextAlign = SKTextAlign.Center;
            titlePaint.SubpixelText = true;

            using var headingFont = new SKFont(SKTypeface.Default, 24.0f);
            using var headingPaint = new SKPaint(headingFont);
            headingPaint.ColorF = textColor;
            headingPaint.IsAntialias = true;
            headingPaint.SubpixelText = true;

            using var entryFont = new SKFont(SKTypeface.Default, 13.0f);
            using var entryPaint = new SKPaint(entryFont);
            entryPaint.ColorF = textColor;
            entryPaint.IsAntialias = true;
            entryPaint.SubpixelText = true;

            float yOffset = 0.0f;
            // Title
            foreach (string line in WrapLines(title, Width, titlePaint)) {
                canvas.DrawText(line, Width / 2.0f, yOffset + titleFont.Offset(), titlePaint);
                yOffset += titleFont.LineHeight();
            }
            yOffset += titleFont.LineHeight() * 0.25f;
            canvas.DrawLine(0.0f, yOffset, Width, yOffset, titlePaint);
            yOffset += titleFont.LineHeight() * 0.5f;

            foreach ((string categoryName, var entries) in categories) {
                // Heading
                foreach (string line in WrapLines(categoryName, Width, headingPaint)) {
                    canvas.DrawText(line, 0.0f, yOffset + headingFont.Offset(), headingPaint);
                    yOffset += headingFont.LineHeight();
                }
                yOffset += headingFont.LineHeight() * 0.5f;

                // Entries
                foreach (string entry in entries) {
                    canvas.DrawCircle(10.0f, yOffset + entryFont.LineHeight() / 2.0f, 2.5f, entryPaint);

                    foreach (string line in WrapLines(entry, Width, entryPaint)) {
                        canvas.DrawText(line, 20.0f, yOffset + entryFont.Offset(), entryPaint);
                        yOffset += entryFont.LineHeight();
                    }
                }

                yOffset += headingFont.LineHeight() * 0.5f;
            }

            Height = (int)yOffset;
        }

        // Doesn't use SKPaint.BreakText(), since that doesn't respect word boundaries
        private IEnumerable<string> WrapLines(string longLine, float maxWidth, SKPaint textPaint) {
            float lineLength = 0.0f;
            var line = new StringBuilder();

            foreach (string word in longLine.Split(' ')) {
                string wordWithSpace = word + " ";
                float wordWithSpaceLength = textPaint.MeasureText(wordWithSpace);

                if (lineLength + wordWithSpaceLength > maxWidth) {
                    yield return line.ToString();
                    line.Clear();
                    line.Append(wordWithSpace);
                    lineLength = wordWithSpaceLength;
                } else {
                    line.Append(wordWithSpace);
                    lineLength += wordWithSpaceLength;
                }
            }

            if (line.Length != 0) {
                yield return line.ToString();
            }
        }
    }

    private ChangelogDialog(string title, IEnumerable<string> changelogLines) {
        // Parse changelog information, generated by Scripts/generate_release.py
        const string categoryPrefix = "## ";
        const string entryPrefix = "- ";

        Dictionary<string, List<string>> categories = [];
        List<string> currentEntries = [];

        foreach (string line in changelogLines) {
            if (line.StartsWith(categoryPrefix)) {
                categories[line[categoryPrefix.Length..].Trim()] = currentEntries = [];
            } else if (line.StartsWith(entryPrefix)) {
                currentEntries.Add(line[entryPrefix.Length..].Trim());
            }
        }

        Title = $"What's new in {title}?";
        Content = new Scrollable {
            Content = new ContentDrawable(title, categories),
            Padding = new Padding(20, 10),
            Border = BorderType.None,
        };
        Padding = 0;
        MinimumSize = new Size(400, 300);
        Size = new Size(800, 600);

        Resizable = true;

        Studio.RegisterDialog(this);
    }

    public static void Show() {
        var asm = Assembly.GetExecutingAssembly();
        using var versionInfoData = asm.GetManifestResourceStream("VersionInfo.txt");
        using var changelogData = asm.GetManifestResourceStream("Changelog.md");
        if (versionInfoData == null || changelogData == null) {
            return;
        }

        using var versionInfoReader = new StreamReader(versionInfoData);
        using var changelogReader = new StreamReader(changelogData);

        string[] versions = versionInfoReader.ReadToEnd().SplitLines().ToArray();
        string[] changelogLines = changelogReader.ReadToEnd().SplitLines().ToArray();
        string title = $"CelesteTAS {versions[0]} / Studio {versions[1]}";

        if (changelogLines.Length == 0) {
            return;
        }

        new ChangelogDialog(title, changelogLines).ShowModal();
    }
}
