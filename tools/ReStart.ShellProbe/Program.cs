using System.Text.Json;

// Test-only shell handler. It never enters the portable package or opens a
// document. The native open verb must preserve exactly one Unicode path.
if (args.Length < 1 || !File.Exists(args[^1])) return 2;
File.WriteAllText(args[^1] + ".opened", JsonSerializer.Serialize(args));
File.WriteAllText(args[^1] + ".cwd", Environment.CurrentDirectory);
return 0;
