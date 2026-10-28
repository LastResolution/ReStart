# Broadcasts the taskbar settings change so Explorer re-reads ExpandableTaskbar without a restart (run via userrun).
$sig = @'
[DllImport("user32.dll", CharSet = CharSet.Unicode)]
public static extern System.IntPtr SendMessageTimeout(System.IntPtr hWnd, uint Msg, System.UIntPtr wParam, string lParam, uint fuFlags, uint uTimeout, out System.UIntPtr lpdwResult);
'@
Add-Type -Namespace T -Name U -MemberDefinition $sig
$r = [System.UIntPtr]::Zero
[T.U]::SendMessageTimeout([System.IntPtr]0xffff, 0x1A, [System.UIntPtr]::Zero, 'TraySettings', 2, 3000, [ref]$r) | Out-Null
