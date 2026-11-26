using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text.Json;

namespace Minioasis.Api.Tests.Shared.TestDoubles;

/// <summary>
/// An RSA key, made in memory, that stands in for one of the signing keys that Cloudflare Access publishes.
/// </summary>
internal sealed class TestSigningKey
{
    public TestSigningKey(string keyId)
    {
        KeyId = keyId;
        Rsa = RSA.Create(2048);
    }

    /// <summary>The key that every test host publishes unless the test says otherwise.</summary>
    public static TestSigningKey Default { get; } = new("default-key");

    public string KeyId
    {
        get;
    }

    public RSA Rsa
    {
        get;
    }

    /// <summary>The key as a JSON Web Key, in the shape Cloudflare publishes it.</summary>
    public Dictionary<string, object?> ToJsonWebKey(string algorithm = "RS256", string keyType = "RSA")
    {
        var parameters = Rsa.ExportParameters(includePrivateParameters: false);
        return new Dictionary<string, object?>
        {
            ["kid"] = KeyId,
            ["kty"] = keyType,
            ["alg"] = algorithm,
            ["use"] = "sig",
            ["e"] = Base64Url.EncodeToString(parameters.Exponent),
            ["n"] = Base64Url.EncodeToString(parameters.Modulus),
        };
    }

    /// <summary>A key set that publishes <paramref name="keys"/>, as the body of Cloudflare's key address.</summary>
    public static string KeySet(params TestSigningKey[] keys) =>
        JsonSerializer.Serialize(new
        {
            keys = keys.Select(key => key.ToJsonWebKey())
        });
}
