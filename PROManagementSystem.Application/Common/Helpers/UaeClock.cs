namespace PROManagementSystem.Application.Common.Helpers;

/// <summary>
/// Business-day helpers in UAE time (Asia/Dubai, UTC+4, no daylight saving).
/// Use these for "today", due dates and overdue checks instead of UTC dates,
/// which are a day behind between midnight and 04:00 local time.
/// </summary>
public static class UaeClock
{
    private static readonly TimeSpan Offset = TimeSpan.FromHours(4);

    public static DateTime Now => DateTime.UtcNow + Offset;

    public static DateOnly Today => DateOnly.FromDateTime(Now);
}
