namespace FurniSpace.Application.DTOs.Accounts;

public sealed class AccountSuggestItemDto
{
    public Guid AccountId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;
}

public sealed class AccountSuggestResponseDto
{
    public IReadOnlyList<AccountSuggestItemDto> Items { get; set; } = [];
}
