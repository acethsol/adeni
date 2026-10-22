namespace Adeni.Application.Notifications;

/// <summary>
/// Inner notification pipeline (preferences → entitlements → idempotency → channel).
/// Used by background workers; bypasses after-commit / Hangfire enqueue.
/// </summary>
public interface INotificationDeliveryDispatcher : INotificationDispatcher;
