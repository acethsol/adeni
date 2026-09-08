namespace Adeni.Application.Messaging;

public interface IFaqAutoResponder
{
    /// <summary>
    /// Returns an auto-reply body when the customer message matches a known FAQ intent; otherwise null.
    /// </summary>
    Task<string?> TryBuildReplyAsync(
        Guid tenantId,
        string customerMessage,
        CancellationToken cancellationToken = default);
}
