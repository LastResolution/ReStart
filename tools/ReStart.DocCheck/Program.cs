using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ReStart.DocCheck;

/// <summary>Checks authored public API contracts using the selected SDK's syntax parser in its own process.</summary>
internal static class Program
{
    private sealed record Finding(string File, int Line, string? Owner, string Name, string[] Missing);

    /// <summary>Runs coverage or deterministic guard fixtures without loading any application assemblies.</summary>
    /// <param name="args">Repository root followed optionally by --self-test or --report-json and its output path.</param>
    /// <returns>Zero on complete contracts/accepted fixtures; one for missing documentation or an invalid invocation.</returns>
    private static int Main(string[] args)
    {
        if (args.Length == 0) return 1;
        var selfTest = args.Contains("--self-test");
        var sources = selfTest ? Fixtures() : ReadSources(args[0]);
        var failures = new List<Finding>(); var checkedCount = 0;
        foreach (var (file, source) in sources)
        {
            var tree = CSharpSyntaxTree.ParseText(source, path: file);
            foreach (var member in tree.GetRoot().DescendantNodes().OfType<MemberDeclarationSyntax>())
            {
                var callable = member is BaseMethodDeclarationSyntax or DelegateDeclarationSyntax
                    || member is TypeDeclarationSyntax type && (type.ParameterList != null || type.TypeParameterList != null);
                if (!callable || !IsPublic(member)) continue;
                checkedCount++;
                var xml = string.Join('\n', member.GetLeadingTrivia().Select(t => t.GetStructure()).OfType<DocumentationCommentTriviaSyntax>().Select(t => t.ToFullString()));
                XElement docs;
                try { docs = XElement.Parse("<docs>" + Regex.Replace(xml, @"(?m)^\s*///\s?", "") + "</docs>"); }
                catch (System.Xml.XmlException) { docs = new("docs"); }
                if (docs.Element("inheritdoc") != null) continue;
                var missing = new List<string>();
                if (!Has(docs, "summary")) missing.Add("summary");
                var parameters = member switch
                {
                    BaseMethodDeclarationSyntax method => method.ParameterList.Parameters,
                    DelegateDeclarationSyntax d => d.ParameterList.Parameters,
                    TypeDeclarationSyntax t => t.ParameterList?.Parameters ?? default,
                    _ => default
                };
                foreach (var parameter in parameters)
                    if (!Has(docs, "param", parameter.Identifier.ValueText)) missing.Add("param:" + parameter.Identifier.ValueText);
                var typeParameters = member switch
                {
                    MethodDeclarationSyntax method => method.TypeParameterList?.Parameters ?? default,
                    DelegateDeclarationSyntax d => d.TypeParameterList?.Parameters ?? default,
                    TypeDeclarationSyntax t => t.TypeParameterList?.Parameters ?? default,
                    _ => default
                };
                foreach (var parameter in typeParameters)
                    if (!Has(docs, "typeparam", parameter.Identifier.ValueText)) missing.Add("typeparam:" + parameter.Identifier.ValueText);
                var returnType = member switch { MethodDeclarationSyntax m => m.ReturnType, DelegateDeclarationSyntax d => d.ReturnType, OperatorDeclarationSyntax o => o.ReturnType, _ => null };
                if (returnType != null && returnType.ToString() != "void" && !Has(docs, "returns")) missing.Add("returns");
                var name = member switch
                {
                    MethodDeclarationSyntax m => m.Identifier.ValueText,
                    ConstructorDeclarationSyntax c => c.Identifier.ValueText,
                    TypeDeclarationSyntax t => t.Identifier.ValueText,
                    DelegateDeclarationSyntax d => d.Identifier.ValueText,
                    OperatorDeclarationSyntax o => o.OperatorToken.ValueText,
                    _ => member.Kind().ToString()
                };
                if (missing.Count > 0) failures.Add(new(file, member.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                    member.Ancestors().OfType<BaseTypeDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText, name, missing.ToArray()));
            }
        }
        if (selfTest)
        {
            var expected = new[] { "Packet:param:Name", "Reader:param:filePath", "Echo:param:value,typeparam:T,returns", "Empty:param:count" };
            if (checkedCount != 5 || !failures.Select(f => f.Name + ":" + string.Join(',', f.Missing)).SequenceEqual(expected)) return 1;
            Console.WriteLine("PASS: complete docs/internal implementations accepted; missing and empty record, primary-constructor, method and generic parameter contracts rejected.");
            return 0;
        }
        var report = Array.IndexOf(args, "--report-json");
        if (report >= 0 && report + 1 < args.Length)
        {
            File.WriteAllText(args[report + 1], JsonSerializer.Serialize(new { Checked = checkedCount, Failures = failures }, new JsonSerializerOptions { WriteIndented = true }));
            return checkedCount > 0 ? 0 : 1;
        }
        foreach (var failure in failures) Console.Error.WriteLine($"{failure.File}:{failure.Line} {failure.Name}: {string.Join(", ", failure.Missing)}");
        if (checkedCount == 0 || failures.Count > 0) return 1;
        Console.WriteLine($"PASS: {checkedCount} public API declarations document their parameters and return values.");
        return 0;
    }

    /// <summary>Tests explicit public visibility through the complete containing-type chain.</summary>
    /// <param name="member">Authored callable or primary/generic type declaration.</param>
    /// <returns>Whether the declaration is public outside its assembly.</returns>
    private static bool IsPublic(MemberDeclarationSyntax member) =>
        (member.Modifiers.Any(SyntaxKind.PublicKeyword) || member.Parent is InterfaceDeclarationSyntax)
        && member.Ancestors().OfType<BaseTypeDeclarationSyntax>().All(t => t.Modifiers.Any(SyntaxKind.PublicKeyword));

    /// <summary>Rejects empty tags while accepting meaningful XML references as documentation content.</summary>
    /// <param name="docs">Parsed documentation comment root.</param>
    /// <param name="kind">Required XML element name.</param>
    /// <param name="name">Optional parameter identity; null checks an unnamed summary/return tag.</param>
    /// <returns>Whether a matching tag has text or a see/parameter reference.</returns>
    private static bool Has(XElement docs, string kind, string? name = null) => docs.Elements(kind).Any(e =>
        (name == null || (string?)e.Attribute("name") == name) && (!string.IsNullOrWhiteSpace(e.Value) || e.Descendants().Any(n => n.Name.LocalName is "see" or "paramref" or "typeparamref")));

    /// <summary>Reads tracked application sources only; generated output and private artifacts are excluded.</summary>
    /// <param name="root">Repository root used as the Git working directory.</param>
    /// <returns>Relative paths and UTF-8 C# source, in Git's stable listing order.</returns>
    private static IEnumerable<(string File, string Source)> ReadSources(string root)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = root, RedirectStandardOutput = true, UseShellExecute = false };
        foreach (var value in new[] { "ls-files", "--", "*.cs" }) start.ArgumentList.Add(value);
        using var git = Process.Start(start) ?? throw new IOException("Git could not start.");
        var files = git.StandardOutput.ReadToEnd(); git.WaitForExit();
        if (git.ExitCode != 0) throw new IOException("Git source listing failed.");
        return files.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(f => f.TrimEnd('\r')).Where(f => f.StartsWith("src/", StringComparison.Ordinal))
            .Select(f => (f, File.ReadAllText(Path.Combine(root, f))));
    }

    /// <summary>Provides fictional positive/negative syntax without changing tracked or personal files.</summary>
    /// <returns>One complete and one intentionally incomplete source fixture.</returns>
    private static IEnumerable<(string File, string Source)> Fixtures() => new[]
    {
        ("positive.cs", """
        public static class Documented {
            /// <summary>Returns the original value.</summary>
            /// <typeparam name="T">Value type.</typeparam>
            /// <param name="value">Value to retain.</param>
            /// <returns>The original value.</returns>
            public static T Echo<T>(T value) => value;
        }
        internal class Hidden { public void InternalImplementation(int ignored) {} }
        """),
        ("negative.cs", """
        /// <summary>A named message.</summary>
        public record Packet(string Name);
        /// <summary>A file reader.</summary>
        public class Reader(string filePath) {}
        public static class Incomplete {
            /// <summary>Returns the original value.</summary>
            public static T Echo<T>(T value) => value;
            /// <summary>Receives a count.</summary>
            /// <param name="count">   </param>
            /// <remarks>A later XML tag must not disguise an empty parameter.</remarks>
            public static void Empty(int count) {}
        }
        """)
    };
}
