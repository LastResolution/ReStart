namespace ReStart.Core;

/// <summary>Identifies a layout written with an unsupported schema, independently of translated error text.</summary>
/// <param name="version">The schema version found in the source document.</param>
public sealed class UnsupportedLayoutVersionException(int version) : IOException(L.F(K.ErrorLayoutVersion, version))
{
    /// <summary>Gets the unsupported schema version. The source file must not be overwritten.</summary>
    public int Version { get; } = version;
}
