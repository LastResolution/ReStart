namespace ReStart.App.Services;

internal sealed partial class IndexedFileSearch
{
    internal static async Task<int> VerifyOwnedFieldsAsync()
    {
        var checks = 0;
        for (var iteration = 0; iteration < 6; iteration++)
        {
            checks += await AppShellCommands.OnSta(() =>
            {
                object? rows = null, fields = null;
                try
                {
                    rows = Activator.CreateInstance(Type.GetTypeFromProgID("ADODB.Recordset", true)!);
                    Set(rows!, "CursorLocation", 3);
                    fields = Get(rows!, "Fields")!;
                    Call(fields, "Append", "System.ItemUrl", 202, 1024);
                    Call(fields, "Append", "System.ItemNameDisplay", 202, 1024);
                    Call(rows!, "Open", Type.Missing, Type.Missing, 1, 3, -1);
                    var name = "Owned résumé 日本語 '[]_% " + iteration;
                    const string uri = "file:///C:/ReStart-Owned-Fixture/Example.txt";
                    Call(rows!, "AddNew", new object[] { "System.ItemUrl", "System.ItemNameDisplay" }, new object[] { uri, name });
                    Call(rows!, "Update"); Call(rows!, "MoveFirst");
                    if (ReadField(fields, 0) != uri || ReadField(fields, 1) != name) throw new InvalidOperationException("The owned ADO field reader changed a URL or Unicode filename.");
                    return 2;
                }
                finally { Release(fields); Close(rows); }
            });
        }
        return checks;
    }
}
