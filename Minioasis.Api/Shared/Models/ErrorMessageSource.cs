namespace Minioasis.Api.Shared.Models;

/// <summary>
/// The bytes of the client-visible error messages (ER-011). By default the embedded <c>Resources/errors.en.json</c>;
/// a test host may register another source.
/// </summary>
public sealed class ErrorMessageSource(ReadOnlyMemory<byte> content)
{
    private const string ResourceName = "Minioasis.Api.Resources.errors.en.json";

    public ReadOnlyMemory<byte> Content { get; } = content;

    /// <summary>Reads <c>Resources/errors.en.json</c>, which the project embeds in this assembly.</summary>
    public static ErrorMessageSource FromEmbeddedResource()
    {
        using var stream = typeof(ErrorMessageSource).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"The embedded resource '{ResourceName}' is missing.");
        using var content = new MemoryStream();
        stream.CopyTo(content);
        return new ErrorMessageSource(content.ToArray());
    }
}
