namespace Adeni.Infrastructure.Tests.Reporting;

using Adeni.Application.Reporting;
using Adeni.Infrastructure.Reporting;
using Microsoft.Extensions.DependencyInjection;

public sealed class ReportRendererTests
{
    private static readonly DateTimeOffset FixedIssuedAt =
        new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RenderDocumentAsync_receipt_fixture_produces_valid_pdf()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var renderer = scope.ServiceProvider.GetRequiredService<IReportRenderer>();

        var data = new BillingDocumentReportData(
            ReportKind.BillingReceipt,
            "RCP-2026-00042",
            "Fixture Salon",
            "Test Customer",
            "NGN",
            4500m,
            "Online payment",
            FixedIssuedAt,
            [new BillingDocumentLineReportData("Fade", 1m, 4500m, 4500m, 0)]);

        var result = await renderer.RenderDocumentAsync(ReportKind.BillingReceipt, data);

        Assert.True(result.IsSuccess, result.Error?.Message);
        AssertPdfHeader(result.Value!);
        Assert.True(result.Value!.Length > 200);
    }

    [Fact]
    public async Task RenderDocumentAsync_kind_mismatch_fails_validation()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var renderer = scope.ServiceProvider.GetRequiredService<IReportRenderer>();

        var data = new BillingDocumentReportData(
            ReportKind.BillingInvoice,
            "INV-2026-00001",
            "Fixture Salon",
            null,
            "NGN",
            100m,
            null,
            FixedIssuedAt,
            [new BillingDocumentLineReportData("Item", 1m, 100m, 100m, 0)]);

        var result = await renderer.RenderDocumentAsync(ReportKind.BillingReceipt, data);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task RenderDocumentAsync_same_fixture_is_deterministic()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var renderer = scope.ServiceProvider.GetRequiredService<IReportRenderer>();

        var data = new BillingDocumentReportData(
            ReportKind.BillingReceipt,
            "RCP-2026-00042",
            "Fixture Salon",
            "Test Customer",
            "NGN",
            4500m,
            "Online payment",
            FixedIssuedAt,
            [new BillingDocumentLineReportData("Fade", 1m, 4500m, 4500m, 0)]);

        var first = await renderer.RenderDocumentAsync(ReportKind.BillingReceipt, data);
        var second = await renderer.RenderDocumentAsync(ReportKind.BillingReceipt, data);

        Assert.True(first.IsSuccess && second.IsSuccess);
        Assert.Equal(first.Value!.Length, second.Value!.Length);
        Assert.True(first.Value.AsSpan().SequenceEqual(second.Value));
    }

    private static void AssertPdfHeader(byte[] pdf)
    {
        Assert.Equal(0x25, pdf[0]);
        Assert.Equal((byte)'P', pdf[1]);
        Assert.Equal((byte)'D', pdf[2]);
        Assert.Equal((byte)'F', pdf[3]);
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddReportingModule();
        return services.BuildServiceProvider();
    }
}
