using Minioasis.Api.Shared.Errors;
using System.Reflection;
using Xunit;

namespace Minioasis.Api.Tests.Shared.Errors;

public sealed class HttpErrorDefinitionsTests
{
    [Fact]
    public void Api_defines_the_errors_of_http_of_briefs_001_and_002_with_their_statuses()
    {
        Assert.Equal(
            [
                new HttpErrorDefinition("not_found", 404),
                new HttpErrorDefinition("method_not_allowed", 405),
                new HttpErrorDefinition("unauthenticated", 401),
                new HttpErrorDefinition("access_denied", 403),
                new HttpErrorDefinition("request_refused", 403),
            ],
            HttpErrorDefinitions.All);
    }

    [Fact]
    public void The_list_holds_every_declared_error_of_http_exactly_once()
    {
        var declared = typeof(HttpErrorDefinitions)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(HttpErrorDefinition))
            .Select(field => (HttpErrorDefinition)field.GetValue(null)!);

        Assert.Equal(
            declared.Select(error => error.Code).Order(StringComparer.Ordinal),
            HttpErrorDefinitions.All.Select(error => error.Code).Order(StringComparer.Ordinal));
    }
}
