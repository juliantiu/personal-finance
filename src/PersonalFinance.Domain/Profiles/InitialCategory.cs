namespace PersonalFinance.Domain.Profiles;

public enum SystemCategoryKind { Income, Savings, Debt }
public sealed record InitialCategory(Guid Id, string Name, SystemCategoryKind Kind);
