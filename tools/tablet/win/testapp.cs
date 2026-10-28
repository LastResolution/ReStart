// Disposable test window for tablet-edition tests (C# 5, .NET Framework csc; see rog.sh `testapp`):
// close/flick/split tests must never target the user's real apps.
//
//   testapp.exe [title] [#rrggbb] [--ask|--refuse]
//     --ask     closing asks "Save changes?" (Yes/No close, Cancel keeps the window), like an editor
//     --refuse  ignores close requests
using System;
using System.Drawing;
using System.Windows.Forms;

internal static class TestApp
{
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hwnd);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool AttachThreadInput(uint from, uint to, bool attach);
    [System.Runtime.InteropServices.DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();

    [STAThread]
    static void Main(string[] args)
    {
        var title = args.Length > 0 ? args[0] : "Test App";
        var color = args.Length > 1 && args[1].StartsWith("#") ? ColorTranslator.FromHtml(args[1]) : Color.FromArgb(0x2D, 0x6C, 0xDF);
        var ask = Array.IndexOf(args, "--ask") >= 0;
        var refuse = Array.IndexOf(args, "--refuse") >= 0;
        Application.EnableVisualStyles();
        var form = new Form { Text = title, BackColor = color, Width = 1100, Height = 700, StartPosition = FormStartPosition.CenterScreen };
        form.Controls.Add(new Label
        {
            Text = title + (ask ? "\n(asks before closing)" : refuse ? "\n(refuses to close)" : ""),
            Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.White,
            Font = new Font("Segoe UI", 40, FontStyle.Bold),
        });
        // Started from a scheduled task, the window would open behind the foreground app (foreground lock).
        form.Shown += (s, e) =>
        {
            uint pid;
            var other = GetWindowThreadProcessId(GetForegroundWindow(), out pid);
            var me = GetCurrentThreadId();
            if (other != me) AttachThreadInput(me, other, true);
            SetForegroundWindow(form.Handle);
            if (other != me) AttachThreadInput(me, other, false);
        };
        form.FormClosing += (s, e) =>
        {
            if (e.CloseReason != CloseReason.UserClosing) return;
            if (refuse) { e.Cancel = true; return; }
            if (ask) e.Cancel = MessageBox.Show(form, "Save changes to " + title + "?", title, MessageBoxButtons.YesNoCancel) == DialogResult.Cancel;
        };
        Application.Run(form);
    }
}
