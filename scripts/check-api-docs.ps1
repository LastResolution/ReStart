[CmdletBinding()]
param([string]$ReportJson = '', [switch]$SelfTest)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$version = (dotnet --version).Trim()
$sdk = dotnet --list-sdks | Where-Object { $_.StartsWith("$version [") } | Select-Object -First 1
if (-not $sdk -or $sdk -notmatch '\[(.+)\]$') { throw 'The selected .NET SDK could not be located.' }
$roslyn = Join-Path $Matches[1] "$version/Roslyn/bincore"
[Reflection.Assembly]::LoadFrom((Join-Path $roslyn 'Microsoft.CodeAnalysis.dll')) | Out-Null
[Reflection.Assembly]::LoadFrom((Join-Path $roslyn 'Microsoft.CodeAnalysis.CSharp.dll')) | Out-Null

function Has-Documentation([string]$Comment, [string]$Kind, [string]$Name = '') {
    $attribute = if ($Name) { '\s+name="' + [regex]::Escape($Name) + '"' } else { '' }
    $match = [regex]::Match($Comment, '(?s)<' + $Kind + $attribute + '>(?<text>.*?)</' + $Kind + '>')
    if (-not $match.Success) { return $false }
    $text = [regex]::Replace($match.Groups['text'].Value, '(?m)^\s*///\s?', '')
    return -not [string]::IsNullOrWhiteSpace([regex]::Replace($text, '<[^>]+>', '')) -or $text -match '<(?:see|paramref|typeparamref)\b'
}

# Read syntax rather than guessing signatures with regular expressions. Only
# explicitly public APIs (and implicit interface members) are checked; compiler-
# generated record equality/accessors and internal Win32 declarations are not.
$failures = [Collections.Generic.List[object]]::new()
$checked = 0
$sources = if ($SelfTest) {
    @{
        File = 'positive.cs'; Source = @'
public static class Documented {
    /// <summary>Returns the original value.</summary>
    /// <typeparam name="T">Value type.</typeparam>
    /// <param name="value">Value to retain.</param>
    /// <returns>The original value.</returns>
    public static T Echo<T>(T value) => value;
}
internal class Hidden { public void InternalImplementation(int ignored) {} }
'@
    }
    @{
        File = 'negative.cs'; Source = @'
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
'@
    }
} else {
    git -C $repoRoot ls-files -- '*.cs' | Where-Object { $_.StartsWith('src/') } | ForEach-Object {
        @{ File = $_; Source = [IO.File]::ReadAllText((Join-Path $repoRoot $_)) }
    }
}
foreach ($source in $sources) {
    $file = $source.File
    $tree = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText($source.Source)
    foreach ($member in $tree.GetRoot().DescendantNodes()) {
        $callable = $member -is [Microsoft.CodeAnalysis.CSharp.Syntax.BaseMethodDeclarationSyntax] -or $member -is [Microsoft.CodeAnalysis.CSharp.Syntax.DelegateDeclarationSyntax]
        $primaryConstructor = $member -is [Microsoft.CodeAnalysis.CSharp.Syntax.TypeDeclarationSyntax] -and $null -ne $member.ParameterList
        if (-not $callable -and -not $primaryConstructor) { continue }
        $visible = ($member.Modifiers.Text -contains 'public') -or $member.Parent -is [Microsoft.CodeAnalysis.CSharp.Syntax.InterfaceDeclarationSyntax]
        foreach ($ancestor in $member.Ancestors()) {
            if ($ancestor -is [Microsoft.CodeAnalysis.CSharp.Syntax.BaseTypeDeclarationSyntax] -and $ancestor.Modifiers.Text -notcontains 'public') { $visible = $false }
        }
        if (-not $visible) { continue }
        $checked++
        $doc = $member.GetLeadingTrivia().ToFullString()
        if ($doc -match '<inheritdoc\b') { continue }
        $missing = [Collections.Generic.List[string]]::new()
        if (-not (Has-Documentation $doc 'summary')) { $missing.Add('summary') }
        foreach ($parameter in $member.ParameterList.Parameters) {
            $name = $parameter.Identifier.ValueText
            if (-not (Has-Documentation $doc 'param' $name)) { $missing.Add("param:$name") }
        }
        foreach ($parameter in $member.TypeParameterList.Parameters) {
            $name = $parameter.Identifier.ValueText
            if (-not (Has-Documentation $doc 'typeparam' $name)) { $missing.Add("typeparam:$name") }
        }
        if ($member -is [Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax] -and $member.ReturnType.ToString() -ne 'void' -and -not (Has-Documentation $doc 'returns')) { $missing.Add('returns') }
        if ($missing.Count) {
            $owner = $member.Ancestors() | Where-Object { $_ -is [Microsoft.CodeAnalysis.CSharp.Syntax.BaseTypeDeclarationSyntax] } | Select-Object -First 1
            $failures.Add([pscustomobject]@{ File = $file; Line = $member.GetLocation().GetLineSpan().StartLinePosition.Line + 1; Owner = $owner.Identifier.ValueText; Name = $member.Identifier.ValueText; Missing = @($missing) })
        }
    }
}
if ($SelfTest) {
    $actual = @($failures | ForEach-Object { "$($_.Name):$($_.Missing -join ',')" })
    $expected = @('Packet:param:Name', 'Reader:param:filePath', 'Echo:param:value,typeparam:T,returns', 'Empty:param:count')
    if ($checked -ne 5 -or (Compare-Object $expected $actual)) { throw 'Documentation guard failed its positive/negative syntax cases.' }
    Write-Output 'PASS: complete docs and internal implementations accepted; missing record, primary-constructor, method and type parameters rejected.'
    exit 0
}
if ($checked -eq 0) { throw 'No public API declarations were inspected.' }
if ($ReportJson) {
    @{ Checked = $checked; Failures = @($failures) } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $ReportJson -Encoding utf8
} elseif ($failures.Count) {
    $failures | ForEach-Object { Write-Output "$($_.File):$($_.Line) $($_.Name): $($_.Missing -join ', ')" }
    throw "Public API documentation is incomplete for $($failures.Count) declarations."
} else { Write-Output "PASS: $checked public API declarations document their parameters and return values." }
