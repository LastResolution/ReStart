using System.Text.Json;
using System.Windows.Documents;
using ICSharpCode.AvalonEdit.Highlighting;
using ReStart.App.Services;

namespace ReStart.App;

public partial class MainWindow
{
    private async Task<int> VerifyStudioEditingAsync(TileStudioWindow studio, string directory)
    {
        var checks = 0;
        void Check(bool value, string message) { Require(value, message); checks++; }
        using var formatter = new TileSourceFormatter(_scriptPackages.BrowserDataRoot);
        foreach (var (parser, source) in new[] { ("babel", "throw new Error('This source must never execute')"), ("html", "<main><p>ReStart</p><p>Tiles</p></main>"),
            ("css", "body{color:red;background:black}"), ("json", "{\"value\":42,\"nested\":{\"ready\":true}}") })
        {
            var result = await formatter.FormatAsync(source, parser, source.Length / 2);
            Check(result.Text.Contains('\n') && result.Caret >= 0 && result.Caret <= result.Text.Length, "An offline parser failed or lost its caret: " + parser);
            Check(result.Text == (await formatter.FormatAsync(result.Text, parser, result.Caret)).Text, "Formatting was not stable: " + parser);
            if (parser == "json")
            { using var json = JsonDocument.Parse(result.Text); Check(json.RootElement.GetProperty("value").GetInt32() == 42, "Formatting changed JSON data."); }
        }
        var rejected = false;
        try { await formatter.FormatAsync("const = ;", "babel", 0); }
        catch (InvalidDataException) { rejected = true; }
        Check(rejected, "Malformed JavaScript was silently replaced.");
        var original = "let first = 1;\nlet second = 2;\n";
        studio.SourceText = original;
        var editor = studio.CurrentEditor; editor.Select(0, original.Length - 1);
        EditingCommands.TabForward.Execute(null, editor.TextArea);
        Check(editor.Text.Split('\n').Where(line => line.Length > 0).All(line => line.StartsWith("    ", StringComparison.Ordinal)), "Tab did not indent the selected lines.");
        EditingCommands.TabBackward.Execute(null, editor.TextArea);
        Check(editor.Text == original, "Shift+Tab did not restore the selected lines.");
        studio.SourceText = "function render(){return{text:'Ready'}}";
        var unformatted = studio.SourceText;
        await studio.FormatDocumentAsync();
        Check(studio.SourceText.Contains("    return", StringComparison.Ordinal), "The Format document action did not apply its result.");
        editor.Document.UndoStack.Undo();
        Check(studio.SourceText == unformatted, "Formatting was not one undoable edit.");
        foreach (var theme in new[] { ReStart.Core.ThemeMode.Dark, ReStart.Core.ThemeMode.Light })
            foreach (var document in new[] { 0, 1, 2, 3 })
            {
                Document.Preferences.Theme = theme; ApplyAppearance(); studio.PrepareDocumentation("orbit", document);
                var current = studio.CurrentEditor;
                using var highlighter = new DocumentHighlighter(current.Document, current.SyntaxHighlighting);
                Check(Enumerable.Range(1, Math.Min(10, current.Document.LineCount)).Any(line => highlighter.HighlightLine(line).Sections.Count > 0), "A code editor has no syntax tokens.");
                await studio.PreviewAsync(); await studio.FreezeForDocumentationAsync();
                SaveFixtureElement(studio, Path.Combine(directory, $"editor-{document}-{theme}.png"));
            }
        Document.Preferences.Theme = ReStart.Core.ThemeMode.Dark; ApplyAppearance(); studio.PrepareDocumentation("clock", 0); await studio.PreviewAsync();
        return checks;
    }
}
