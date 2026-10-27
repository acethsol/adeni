namespace Adeni.Infrastructure.Finance;

using Adeni.Application.Finance;
using Adeni.Application.Tenancy;
using Adeni.Domain.Finance;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class CustomerCommerceQuery(AdeniDbContext db, ICustomerCommerceBookingQuery bookings) : ICustomerCommerceQuery
{
    private async Task<IQueryable<PaymentIntentRecord>> PaymentsAsync(CustomerProfileScope scope, DateTimeOffset asOf, CancellationToken cancellationToken)
    {
        var ids = await bookings.GetBookingIdsAsync(scope, cancellationToken);
        return db.PaymentIntents.AsNoTracking().Where(x => x.TenantId == scope.TenantId
            && x.BookingId != null && ids.Contains(x.BookingId.Value)
            && x.Status == PaymentIntentStatus.Completed && x.UpdatedAt <= asOf);
    }

    public async Task<IReadOnlyList<CustomerCurrencyTotal>> GetTotalsAsync(CustomerProfileScope scope, DateTimeOffset asOf, CancellationToken cancellationToken)
    {
        var payments = await PaymentsAsync(scope, asOf, cancellationToken);
        // Amount is the recorded payment principal. TipAmount is stored separately; never add it twice or infer provider settlement.
        return await payments.GroupBy(x => x.Currency).OrderBy(x => x.Key)
            .Select(x => new CustomerCurrencyTotal(x.Key, x.Sum(p => p.Amount))).ToArrayAsync(cancellationToken);
    }

    public async Task<CustomerActivityBatch> GetActivityAsync(CustomerProfileScope scope, DateTimeOffset from, DateTimeOffset asOf, CancellationToken cancellationToken)
    {
        var payments = await PaymentsAsync(scope, asOf, cancellationToken);
        var rows = await payments.Where(x => x.UpdatedAt >= from).OrderByDescending(x => x.UpdatedAt).ThenByDescending(x => x.Id)
            .Select(x => new { x.Id, x.UpdatedAt, x.Amount, x.Currency }).Take(101).ToArrayAsync(cancellationToken);
        return new(rows.Take(100).Select(x => new CustomerActivityItem($"finance.payment:{x.Id:N}", "onlinePaymentRecorded",
            x.UpdatedAt, "finance", x.Id, scope.LocationId, Amount: x.Amount, Currency: x.Currency)).ToArray(), rows.Length > 100);
    }
}
