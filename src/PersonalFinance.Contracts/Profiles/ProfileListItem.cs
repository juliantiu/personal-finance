namespace PersonalFinance.Contracts.Profiles;

public sealed record ProfileListItem(Guid Id, string Name, string MainCurrency, string Role);
