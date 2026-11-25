using Minioasis.Application.Shared.Errors;

namespace Minioasis.Application.Items.Errors;

public static class ItemErrorDefinitions
{
    public static readonly ApplicationErrorDefinition InvalidIdentifier =
        new("items.invalid_identifier", ApplicationErrorCategory.Validation);

    public static readonly ApplicationErrorDefinition NotFound =
        new("items.not_found", ApplicationErrorCategory.NotFound);
}
