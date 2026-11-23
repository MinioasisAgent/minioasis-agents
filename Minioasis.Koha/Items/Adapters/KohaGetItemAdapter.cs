using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minioasis.Application.Items.Abstractions;
using Minioasis.Application.Items.Models;
using Minioasis.Application.Items.Outcomes;
using Minioasis.Application.Items.Responses;
using Minioasis.Application.Items.Results;
using Minioasis.Koha.Authentication.Exceptions;
using Minioasis.Koha.Generated.Items;
using Minioasis.Koha.Generated.Items.Contracts;
using Minioasis.Koha.Shared.Http;
using Minioasis.Koha.Shared.Options;
using System.Globalization;

namespace Minioasis.Koha.Items.Adapters;

/// <summary>
/// Reads one item from Koha (<c>getItem</c>) and translates Koha's answer into an outcome (ER-010).
/// </summary>
internal sealed partial class KohaGetItemAdapter(
    IHttpClientFactory httpClientFactory,
    IOptions<KohaOptions> options,
    ILogger<KohaGetItemAdapter> logger) : IGetItemGateway
{
    public async Task<GetItemGatewayResult> GetItemAsync(ItemId itemId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(itemId);

        var client = new GeneratedKohaItemClient(httpClientFactory.CreateClient(KohaClientNames.Api))
        {
            BaseUrl = options.Value.ApiBaseAddress.AbsoluteUri,
        };

        try
        {
            return ToResult(await client.GetItemAsync(itemId.Value, cancellationToken));
        }
        catch (KohaServiceException exception)
        {
            return Failed(Translate(exception), exception.StatusCode);
        }
        catch (KohaAuthenticationException)
        {
            return Failed(GetItemOutcome.AuthenticationFailed, statusCode: null);
        }
        catch (HttpRequestException exception)
        {
            LogUnreachable(exception.HttpRequestError);
            return Failed(GetItemOutcome.Unavailable, statusCode: null);
        }
        catch (TimeoutException)
        {
            return Failed(GetItemOutcome.Unavailable, statusCode: null);
        }
    }

    /// <summary>
    /// The translation table of brief 001, part 3, for an answer of Koha that is not a usable 200.
    /// A 401 arrives here only after the one retry.
    /// </summary>
    private static GetItemOutcome Translate(KohaServiceException exception) => exception.StatusCode switch
    {
        401 or 403 => GetItemOutcome.AuthenticationFailed,
        429 or (>= 500 and <= 599) => GetItemOutcome.Unavailable,
        404 when exception is KohaServiceException<Error> { Result.Error_code: "not_found" } => GetItemOutcome.NotFound,
        _ => GetItemOutcome.InvalidResponse,
    };

    private GetItemGatewayResult ToResult(Item item)
    {
        if (item.Item_id <= 0 || item.Biblio_id <= 0)
        {
            return Failed(GetItemOutcome.InvalidResponse, statusCode: 200);
        }

        return GetItemGatewayResult.Found(new GetItemResponse(
            item.Item_id.ToString(CultureInfo.InvariantCulture),
            item.Biblio_id.ToString(CultureInfo.InvariantCulture),
            item.External_id,
            item.Callnumber));
    }

    private GetItemGatewayResult Failed(GetItemOutcome outcome, int? statusCode)
    {
        if (outcome != GetItemOutcome.NotFound)
        {
            LogFailure(outcome, statusCode);
        }

        return GetItemGatewayResult.Failed(outcome);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Koha getItem failed: {Outcome}, Koha status {StatusCode}.")]
    private partial void LogFailure(GetItemOutcome outcome, int? statusCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Koha getItem could not reach Koha: {HttpRequestError}.")]
    private partial void LogUnreachable(HttpRequestError httpRequestError);
}
