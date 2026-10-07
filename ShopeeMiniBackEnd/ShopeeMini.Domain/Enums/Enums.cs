namespace ShopeeMini.Domain.Enums;

public enum UserRole
{
    User = 0,
    Admin = 1
}

public enum OrderStatus
{
    Pending = 0,
    Paid = 1,
    Failed = 2,
    Cancelled = 3
}

public enum NotificationType
{
    OrderPlacedSuccess = 0,
    OrderPlacedFailed = 1,
    NewOrderForShop = 2,
    ProductHiddenRemovedFromCart = 3
}

public enum NotificationTargetRole
{
    User = 0,
    Admin = 1
}
