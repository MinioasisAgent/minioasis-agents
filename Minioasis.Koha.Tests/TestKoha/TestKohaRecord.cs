using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Minioasis.Koha.Tests.TestKoha;

/// <summary>
/// A bibliographic record with one item, created on the test Koha for one check and deleted by it (TS-013).
/// The calls are handwritten, with paths and fields from the official contract: addBiblio, addBiblioItem,
/// deleteItem, and deleteBiblio. The application never makes them.
/// </summary>
internal sealed class TestKohaRecord : IAsyncDisposable
{
    private readonly HttpClient client;
    private bool itemDeleted;
    private bool biblioDeleted;

    private TestKohaRecord(HttpClient client, int biblioId, int itemId, string barcode, string callNumber)
    {
        this.client = client;
        BiblioId = biblioId;
        ItemId = itemId;
        Barcode = barcode;
        CallNumber = callNumber;
    }

    public int BiblioId
    {
        get;
    }

    public int ItemId
    {
        get;
    }

    public string Barcode
    {
        get;
    }

    public string CallNumber
    {
        get;
    }

    public static async Task<TestKohaRecord> CreateAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var unique = DateTime.UtcNow.ToString("yyMMddHHmmss", CultureInfo.InvariantCulture)
            + Random.Shared.Next(0x10000).ToString("x4", CultureInfo.InvariantCulture);
        var barcode = $"MO{unique}";
        var callNumber = $"MO TEST {unique}";

        var marc = JsonSerializer.Serialize(new
        {
            leader = "00000nam a22000007a 4500",
            fields = new object[]
            {
                new Dictionary<string, object>
                {
                    ["245"] = new { ind1 = "1", ind2 = "0", subfields = new[] { new { a = $"Minioasis test Koha check {unique}" } } },
                },
            },
        });
        using var biblioContent = new StringContent(marc, Encoding.UTF8);
        biblioContent.Headers.ContentType = new("application/marc-in-json");
        using var biblioResponse = await client.PostAsync("biblios", biblioContent, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, biblioResponse.StatusCode);
        var biblioId = (await biblioResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetInt32();

        using var itemResponse = await client.PostAsJsonAsync(
            $"biblios/{biblioId}/items",
            new
            {
                external_id = barcode,
                callnumber = callNumber,
                home_library_id = "TEST",
                holding_library_id = "TEST",
                item_type_id = "TEST",
            },
            cancellationToken);
        if (itemResponse.StatusCode != HttpStatusCode.Created)
        {
            await client.DeleteAsync($"biblios/{biblioId}", CancellationToken.None);
            Assert.Fail($"Koha answered {(int)itemResponse.StatusCode} to the creation of the item.");
        }

        var itemId = (await itemResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("item_id").GetInt32();
        return new TestKohaRecord(client, biblioId, itemId, barcode, callNumber);
    }

    /// <summary>Deletes the item and returns Koha's status.</summary>
    public async Task<HttpStatusCode> DeleteItemAsync()
    {
        using var response = await client.DeleteAsync($"items/{ItemId}", CancellationToken.None);
        itemDeleted |= response.StatusCode == HttpStatusCode.NoContent;
        return response.StatusCode;
    }

    /// <summary>Deletes the bibliographic record and returns Koha's status.</summary>
    public async Task<HttpStatusCode> DeleteBiblioAsync()
    {
        using var response = await client.DeleteAsync($"biblios/{BiblioId}", CancellationToken.None);
        biblioDeleted |= response.StatusCode == HttpStatusCode.NoContent;
        return response.StatusCode;
    }

    /// <summary>Removes whatever the check did not delete itself, for example after a failed assertion.</summary>
    public async ValueTask DisposeAsync()
    {
        if (!itemDeleted)
        {
            await DeleteItemAsync();
        }

        if (!biblioDeleted)
        {
            await DeleteBiblioAsync();
        }
    }
}
