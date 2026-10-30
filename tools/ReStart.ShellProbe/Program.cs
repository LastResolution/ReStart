using System.Text.Json;

// Test-only shell handler. It never enters the portable package or opens a
// document. The native open verb must preserve exactly one Unicode path.
if (args is ["--jump-worker-fault", var fault])
{
    if (fault == "timeout") { Thread.Sleep(10000); return 0; }
    if (fault == "crash") return 19;
    if (fault == "oversized") { Console.Write(new string('x', 300000)); return 0; }
    Console.Write("not a JSON worker reply"); return 0;
}
if (args.Length < 1 || !File.Exists(args[^1])) return 2;
Publish(args[^1] + ".opened", JsonSerializer.Serialize(args));
Publish(args[^1] + ".cwd", Environment.CurrentDirectory);
if (args[0] == "--jump-fixture-hold")
{
    Thread.Sleep(10000);
    Publish(args[^1] + ".survived", "owned child survived helper exit");
}
return 0;

static void Publish(string path, string content)
{
    var temporary = path + ".tmp";
    File.WriteAllText(temporary, content);
    File.Move(temporary, path);
}
