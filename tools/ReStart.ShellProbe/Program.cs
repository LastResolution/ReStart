using System.Text.Json;

// Test-only shell handler. It never enters the portable package or opens a
// document. The native open verb must preserve exactly one Unicode path.
if (args.Length < 1 || !File.Exists(args[^1])) return 2;
Publish(args[^1] + ".opened", JsonSerializer.Serialize(args));
Publish(args[^1] + ".cwd", Environment.CurrentDirectory);
return 0;

static void Publish(string path, string content)
{
    var temporary = path + ".tmp";
    File.WriteAllText(temporary, content);
    File.Move(temporary, path);
}
