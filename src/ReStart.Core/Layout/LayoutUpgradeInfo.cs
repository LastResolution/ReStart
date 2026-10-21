namespace ReStart.Core;

/// <summary>Records a completed local upgrade and the backup needed to restore the prior application or schema.</summary>
/// <param name="FromAppVersion">Application version that last saved the input layout, or empty when unknown.</param>
/// <param name="ToAppVersion">Application version performing the upgrade.</param>
/// <param name="FromSchema">Input layout schema version.</param>
/// <param name="ToSchema">Schema version written by the upgrade.</param>
/// <param name="AtUtc">UTC time when the upgrade was recorded.</param>
/// <param name="BackupFile">Local backup filename, without its containing directory.</param>
public sealed record LayoutUpgradeInfo(string FromAppVersion, string ToAppVersion, int FromSchema, int ToSchema, DateTimeOffset AtUtc, string BackupFile);
