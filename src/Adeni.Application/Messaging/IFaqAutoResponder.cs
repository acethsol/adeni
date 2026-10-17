namespace Adeni.Application.Messaging;

public interface IFaqAutoResponder
{
    Task<string?> TryBuildReplyAsync(
        Guid tenantId,
        string customerMessage,
        CancellationToken cancellationToken = default);
}
