namespace ReStart.Core;

/// <summary>Requested application palette; high contrast always takes precedence.</summary>
public enum ThemeMode
{
    /// <summary>Follow the Windows application theme.</summary>
    System,
    /// <summary>Use the dark palette.</summary>
    Dark,
    /// <summary>Use the light palette.</summary>
    Light
}
