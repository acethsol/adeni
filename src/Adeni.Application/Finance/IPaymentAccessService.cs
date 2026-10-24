namespace Adeni.Application.Finance;

public interface IPaymentAccessService
{
    Task<bool> CanAccessAsync(
        PaymentIntentResponse payment,
        string? customerAuth0Sub,
        string? businessAuth0Sub,
        CancellationToken cancellationToken = default);
}
