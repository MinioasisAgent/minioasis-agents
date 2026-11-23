using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Minioasis.Koha.Tests.Shared.TestDoubles;

/// <summary>
/// Koha's answers, shaped as the test Koha (26.05) sends them.
/// </summary>
internal static class KohaResponses
{
    /// <summary>The content type that the test Koha sends with a 401 and a 403.</summary>
    public const string Utf8WithoutHyphen = "application/json; charset=utf8";

    public static HttpResponseMessage Json(HttpStatusCode status, string json, string contentType = "application/json")
    {
        var content = new ByteArrayContent(Encoding.UTF8.GetBytes(json));
        content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        return new HttpResponseMessage(status) { Content = content };
    }

    public static HttpResponseMessage Token(string token = "token-1", int expiresIn = 3600, string tokenType = "Bearer") =>
        Json(
            HttpStatusCode.OK,
            JsonSerializer.Serialize(new
            {
                access_token = token,
                token_type = tokenType,
                expires_in = expiresIn
            }));

    public static HttpResponseMessage Item(int itemId, int biblioId, string? barcode, string? callNumber) =>
        Json(HttpStatusCode.OK, ItemJson(itemId, biblioId, barcode, callNumber));

    public static string ItemJson(int itemId, int biblioId, string? barcode, string? callNumber) =>
        JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["item_id"] = itemId,
            ["biblio_id"] = biblioId,
            ["external_id"] = barcode,
            ["callnumber"] = callNumber,
            ["acquisition_date"] = "2026-11-23",
            ["damaged_status"] = 0,
            ["home_library_id"] = "TEST",
            ["timestamp"] = "2026-11-23T08:00:00+00:00",
        });

    public static HttpResponseMessage ItemNotFound() =>
        Json(HttpStatusCode.NotFound, """{"error":"Item not found","error_code":"not_found"}""");

    public static HttpResponseMessage Status(HttpStatusCode status) => new(status);
}
