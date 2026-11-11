namespace PersonalFinance.Infrastructure.Persistence.Profiles;

public sealed class SavingsPlanRecord
{
    public Guid SubcategoryId { get; set; }
    public decimal? GoalAmount { get; set; }
    public string? ResetNote { get; set; }
}
