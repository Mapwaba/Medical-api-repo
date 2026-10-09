namespace LandaDoc.Shared.DTOs;

public record NotificationDto(
    Guid Id, string Type, string Title, string Body, bool IsRead, DateTime CreatedAt
);

// One page of a user's notifications, newest first, with the totals the page needs for its
// pager and counts. PageSizes are the sizes the apps offer; any other is treated as 20.
public record NotificationPageDto(List<NotificationDto> Items, int TotalCount, int UnreadCount, int Page, int PageSize)
{
    public static readonly int[] PageSizes = [10, 20, 30, 50, 100];
    public const int DefaultPageSize = 20;
}
