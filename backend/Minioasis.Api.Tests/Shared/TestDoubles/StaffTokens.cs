using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Minioasis.Api.Tests.Shared.TestDoubles;

/// <summary>
/// Builds the tokens that Cloudflare Access would issue, and broken ones, with full control over every part. The
/// settings of every test host match <see cref="TeamDomain"/>, <see cref="Audience"/>, and <see cref="Email"/>.
/// </summary>
internal static class StaffTokens
{
    public const string TeamDomain = "https://example.cloudflareaccess.com";
    public const string Audience = "test-audience-tag";
    public const string Email = "staff@example.org";

    /// <summary>
    /// A token for the configured email, valid for ten minutes, signed by <paramref name="key"/> (the default key when
    /// null). <paramref name="payload"/> and <paramref name="header"/> may change or remove any claim before signing.
    /// </summary>
    public static string Create(
        TestSigningKey? key = null,
        Action<Dictionary<string, object?>>? payload = null,
        Action<Dictionary<string, object?>>? header = null)
    {
        key ??= TestSigningKey.Default;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var claims = new Dictionary<string, object?>
        {
            ["aud"] = new[] { Audience },
            ["email"] = Email,
            ["exp"] = now + 600,
            ["iat"] = now,
            ["nbf"] = now,
            ["iss"] = TeamDomain,
            ["type"] = "app",
            ["sub"] = "00000000-0000-0000-0000-000000000001",
        };
        var headers = new Dictionary<string, object?> { ["alg"] = "RS256", ["kid"] = key.KeyId, ["typ"] = "JWT" };
        payload?.Invoke(claims);
        header?.Invoke(headers);

        var signingInput = Encode(headers) + "." + Encode(claims);
        return signingInput + "." + Base64Url.EncodeToString(SignRs256(signingInput, key));
    }

    /// <summary>A token signed with HS256, using <paramref name="secret"/> as the shared secret.</summary>
    public static string CreateHs256(byte[] secret)
    {
        var valid = Create();
        var parts = valid.Split('.');
        var header = Encode(new Dictionary<string, object?> { ["alg"] = "HS256", ["kid"] = TestSigningKey.Default.KeyId, ["typ"] = "JWT" });
        var signingInput = header + "." + parts[1];
        return signingInput + "." + Base64Url.EncodeToString(HMACSHA256.HashData(secret, Encoding.ASCII.GetBytes(signingInput)));
    }

    /// <summary>An unsigned token, with the algorithm <c>none</c> and an empty signature.</summary>
    public static string CreateUnsigned()
    {
        var parts = Create().Split('.');
        var header = Encode(new Dictionary<string, object?> { ["alg"] = "none", ["typ"] = "JWT" });
        return header + "." + parts[1] + ".";
    }

    /// <summary>A valid token whose payload is replaced by another one, under the first one's signature.</summary>
    public static string WithChangedPayload()
    {
        var signed = Create().Split('.');
        var changed = Create(payload: claims => claims["email"] = "other@example.org").Split('.');
        return signed[0] + "." + changed[1] + "." + signed[2];
    }

    /// <summary>The payload part of a token: its second part.</summary>
    public static string PayloadOf(string token) => token.Split('.')[1];

    private static string Encode(Dictionary<string, object?> part) =>
        Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(part));

    private static byte[] SignRs256(string signingInput, TestSigningKey key) =>
        key.Rsa.SignData(Encoding.ASCII.GetBytes(signingInput), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
}
