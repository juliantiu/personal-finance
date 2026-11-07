namespace PersonalFinance.Domain.Profiles;

public enum SystemTagRole { Iou, IouRepayment }
public sealed record InitialTag(Guid Id, string Name, SystemTagRole Role);
