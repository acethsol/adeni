namespace Adeni.Infrastructure.Tests.Finance;

using Adeni.Application.Abstractions;
using Adeni.Application.Finance;
using Adeni.Domain.Booking;
using Adeni.Domain.Finance;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Finance;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public sealed class StaffCompensationServiceTests
{
    [Theory]
    [InlineData("Alex", "Alex")]
    [InlineData("Àda, Guest", "\"Àda, Guest\"")]
    [InlineData("=1+1", "'=1+1")]
    [InlineData("A\rB", "\"A\rB\"")]
    public async Task ExportAsync_returns_csv_for_till_tip_and_commission(string staffName, string expectedName)
    {
        var tenantId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var serviceOfferingId = Guid.NewGuid();
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IStaffCompensationService>();

        db.BusinessProfiles.Add(new BusinessProfile
        {
            TenantId = tenantId,
            Description = "Test",
            CategorySlug = "salon",
            Phone = "+1234567890",
            StaffCommissionPercent = 10,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        db.StaffMembers.Add(new StaffMember
        {
            Id = staffId,
            TenantId = tenantId,
            DisplayName = staffName,
            RoleKey = "stylist",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        db.ServiceOfferings.Add(new ServiceOffering
        {
            Id = serviceOfferingId,
            TenantId = tenantId,
            Name = "Cut",
            PriceAmount = 100m,
            Currency = "NGN",
            DurationMinutes = 60,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        db.Bookings.Add(new BookingRecord
        {
            Id = bookingId,
            TenantId = tenantId,
            ServiceOfferingId = serviceOfferingId,
            StaffMemberId = staffId,
            Status = BookingStatus.Paid,
            StartAt = DateTimeOffset.UtcNow,
            EndAt = DateTimeOffset.UtcNow.AddHours(1),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        await service.RecordTipForTillPaymentAsync(tenantId, bookingId, 15m, staffId, "NGN");
        await service.AccrueCommissionForPaidBookingAsync(tenantId, bookingId);

        var from = DateTimeOffset.UtcNow.AddDays(-1);
        var to = DateTimeOffset.UtcNow.AddDays(1);
        var export = await service.ExportAsync(tenantId, from, to, CompensationExportFormat.Csv);

        Assert.True(export.IsSuccess);
        var text = System.Text.Encoding.UTF8.GetString(export.Value!.Body);
        Assert.Contains(expectedName, text);
        Assert.Contains("tip", text);
        Assert.Contains("commission", text);
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITenantContext, NoTenantContext>();
        services.AddDbContext<AdeniDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddScoped<IStaffCompensationService, StaffCompensationService>();
        return services.BuildServiceProvider();
    }

    private sealed class NoTenantContext : ITenantContext
    {
        public TenantId? CurrentTenantId => null;
        public bool IsTenantFilterActive => false;
        public void EnableTenantFilter(TenantId tenantId) { }
        public void DisableTenantFilter() { }
        public void Set(TenantId tenantId) { }
        public void Clear() { }
    }
}
