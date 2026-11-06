// Comment ID: CLASS-057. Identifies each initial IOU tag by a stable role rather than its display name.
namespace PersonalFinance.Domain.Profiles;

public enum SystemTagRole { Iou, IouRepayment }
public sealed record InitialTag(Guid Id, string Name, SystemTagRole Role);
