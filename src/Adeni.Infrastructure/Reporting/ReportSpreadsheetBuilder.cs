namespace Adeni.Infrastructure.Reporting;

using System.Globalization;
using System.Text;
using Adeni.Application.Finance;
using Adeni.Application.Reporting;
using ClosedXML.Excel;
using Adeni.Infrastructure.Serialization;

internal static class ReportSpreadsheetBuilder
{
    public static byte[] BuildBookingsRegisterXlsx(IReadOnlyList<BookingsRegisterExportRow> rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Bookings");
        var headers = new[]
        {
            "BookingId",
            "StartAt",
            "EndAt",
            "Status",
            "Service",
            "Customer",
            "Staff",
            "Total",
            "Currency",
        };

        for (var col = 0; col < headers.Length; col++)
        {
            sheet.Cell(1, col + 1).Value = headers[col];
        }

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var line = i + 2;
            sheet.Cell(line, 1).Value = row.BookingId.ToString();
            sheet.Cell(line, 2).Value = row.StartAt.ToString("O", CultureInfo.InvariantCulture);
            sheet.Cell(line, 3).Value = row.EndAt.ToString("O", CultureInfo.InvariantCulture);
            sheet.Cell(line, 4).Value = row.Status;
            sheet.Cell(line, 5).Value = row.ServiceName;
            sheet.Cell(line, 6).Value = row.CustomerName ?? string.Empty;
            sheet.Cell(line, 7).Value = row.StaffDisplayName ?? string.Empty;
            if (row.TotalAmount is { } amount)
            {
                sheet.Cell(line, 8).Value = amount;
            }

            sheet.Cell(line, 9).Value = row.Currency ?? string.Empty;
        }

        sheet.Row(1).Style.Font.Bold = true;
        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public static byte[] BuildCompensationXlsx(IReadOnlyList<StaffCompensationExportRow> rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Compensation");
        var headers = new[]
        {
            "CreatedAt",
            "StaffMemberId",
            "StaffName",
            "Type",
            "Amount",
            "Currency",
            "BookingId",
            "PaymentIntentId",
        };

        for (var col = 0; col < headers.Length; col++)
        {
            sheet.Cell(1, col + 1).Value = headers[col];
        }

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var line = i + 2;
            sheet.Cell(line, 1).Value = row.CreatedAt.ToString("O", CultureInfo.InvariantCulture);
            sheet.Cell(line, 2).Value = row.StaffMemberId.ToString();
            sheet.Cell(line, 3).Value = row.StaffDisplayName;
            sheet.Cell(line, 4).Value = row.Type;
            sheet.Cell(line, 5).Value = row.Amount;
            sheet.Cell(line, 6).Value = row.Currency;
            sheet.Cell(line, 7).Value = row.BookingId?.ToString() ?? string.Empty;
            sheet.Cell(line, 8).Value = row.PaymentIntentId?.ToString() ?? string.Empty;
        }

        sheet.Row(1).Style.Font.Bold = true;
        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public static byte[] BuildBookingsRegisterCsv(IReadOnlyList<BookingsRegisterExportRow> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("BookingId,StartAt,EndAt,Status,Service,Customer,Staff,Total,Currency");
        foreach (var row in rows)
        {
            builder.Append(CsvField.Format(row.BookingId.ToString()));
            builder.Append(',');
            builder.Append(CsvField.Format(row.StartAt.ToString("O", CultureInfo.InvariantCulture)));
            builder.Append(',');
            builder.Append(CsvField.Format(row.EndAt.ToString("O", CultureInfo.InvariantCulture)));
            builder.Append(',');
            builder.Append(CsvField.Format(row.Status));
            builder.Append(',');
            builder.Append(CsvField.Format(row.ServiceName));
            builder.Append(',');
            builder.Append(CsvField.Format(row.CustomerName));
            builder.Append(',');
            builder.Append(CsvField.Format(row.StaffDisplayName));
            builder.Append(',');
            builder.Append(CsvField.Format(row.TotalAmount));
            builder.Append(',');
            builder.AppendLine(CsvField.Format(row.Currency));
        }

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    public static string BuildStaffRosterCsv(IReadOnlyList<StaffRosterExportRow> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("DisplayName,RoleKey,IsActive,PortalAccess,ServiceCount,SortOrder");
        foreach (var row in rows)
        {
            builder.Append(CsvField.Format(row.DisplayName));
            builder.Append(',');
            builder.Append(CsvField.Format(row.RoleKey));
            builder.Append(',');
            builder.Append(row.IsActive ? "true" : "false");
            builder.Append(',');
            builder.Append(CsvField.Format(row.PortalAccessStatus));
            builder.Append(',');
            builder.Append(row.ServiceCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.AppendLine(row.SortOrder.ToString(CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    public static string BuildAttendanceRegisterCsv(IReadOnlyList<AttendanceRegisterExportRow> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("OccurredAt,StaffDisplayName,EventType,LocationName,LocationVerificationStatus,VerificationMethod,DistanceFromLocationMeters");
        foreach (var row in rows)
        {
            builder.Append(CsvField.Format(row.OccurredAt.ToString("O")));
            builder.Append(',');
            builder.Append(CsvField.Format(row.StaffDisplayName));
            builder.Append(',');
            builder.Append(CsvField.Format(row.EventType));
            builder.Append(',');
            builder.Append(CsvField.Format(row.LocationName));
            builder.Append(',');
            builder.Append(CsvField.Format(row.LocationVerificationStatus));
            builder.Append(',');
            builder.Append(CsvField.Format(row.VerificationMethod));
            builder.Append(',');
            builder.AppendLine(row.DistanceFromLocationMeters?.ToString(CultureInfo.InvariantCulture) ?? "");
        }

        return builder.ToString();
    }

    public static string BuildTenantCustomersCsv(IReadOnlyList<TenantCustomerExportRow> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Id,DisplayName,PhoneMasked,Email,ProfileType,LastSeenAt,CreatedAt");
        foreach (var row in rows)
        {
            builder.Append(row.Id);
            builder.Append(',');
            builder.Append(CsvField.Format(row.DisplayName));
            builder.Append(',');
            builder.Append(CsvField.Format(row.PhoneMasked));
            builder.Append(',');
            builder.Append(CsvField.Format(row.Email));
            builder.Append(',');
            builder.Append(CsvField.Format(row.ProfileType));
            builder.Append(',');
            builder.Append(row.LastSeenAt.ToString("O", CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.AppendLine(row.CreatedAt.ToString("O", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    public static byte[] BuildTenantCustomersXlsx(IReadOnlyList<TenantCustomerExportRow> rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Customers");
        var headers = new[]
        {
            "Id",
            "DisplayName",
            "PhoneMasked",
            "Email",
            "ProfileType",
            "LastSeenAt",
            "CreatedAt",
        };

        for (var col = 0; col < headers.Length; col++)
        {
            sheet.Cell(1, col + 1).Value = headers[col];
        }

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var line = i + 2;
            sheet.Cell(line, 1).Value = row.Id.ToString();
            sheet.Cell(line, 2).Value = row.DisplayName;
            sheet.Cell(line, 3).Value = row.PhoneMasked;
            sheet.Cell(line, 4).Value = row.Email ?? string.Empty;
            sheet.Cell(line, 5).Value = row.ProfileType;
            sheet.Cell(line, 6).Value = row.LastSeenAt.ToString("O", CultureInfo.InvariantCulture);
            sheet.Cell(line, 7).Value = row.CreatedAt.ToString("O", CultureInfo.InvariantCulture);
        }

        sheet.Row(1).Style.Font.Bold = true;
        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public static string BuildTenantLocationsCsv(IReadOnlyList<TenantLocationExportRow> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Slug,Name,MarketId,AddressLine,Area,IsPrimary,IsActive");
        foreach (var row in rows)
        {
            builder.Append(CsvField.Format(row.Slug));
            builder.Append(',');
            builder.Append(CsvField.Format(row.Name));
            builder.Append(',');
            builder.Append(CsvField.Format(row.MarketId));
            builder.Append(',');
            builder.Append(CsvField.Format(row.AddressLine));
            builder.Append(',');
            builder.Append(CsvField.Format(row.Area));
            builder.Append(',');
            builder.Append(row.IsPrimary ? "true" : "false");
            builder.Append(',');
            builder.AppendLine(row.IsActive ? "true" : "false");
        }

        return builder.ToString();
    }

    public static string BuildTenantServicesCsv(IReadOnlyList<TenantServiceExportRow> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Name,Description,PriceAmount,Currency,DurationMinutes,CategorySlug,IsActive,IsAddOn,SortOrder");
        foreach (var row in rows)
        {
            builder.Append(CsvField.Format(row.Name));
            builder.Append(',');
            builder.Append(CsvField.Format(row.Description));
            builder.Append(',');
            builder.Append(row.PriceAmount.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(CsvField.Format(row.Currency));
            builder.Append(',');
            builder.Append(row.DurationMinutes.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(CsvField.Format(row.CategorySlug));
            builder.Append(',');
            builder.Append(row.IsActive ? "true" : "false");
            builder.Append(',');
            builder.Append(row.IsAddOn ? "true" : "false");
            builder.Append(',');
            builder.AppendLine(row.SortOrder.ToString(CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    public static byte[] BuildTenantServicesXlsx(IReadOnlyList<TenantServiceExportRow> rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Services");
        var headers = new[]
        {
            "Name",
            "Description",
            "PriceAmount",
            "Currency",
            "DurationMinutes",
            "CategorySlug",
            "IsActive",
            "IsAddOn",
            "SortOrder",
        };

        for (var col = 0; col < headers.Length; col++)
        {
            sheet.Cell(1, col + 1).Value = headers[col];
        }

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var line = i + 2;
            sheet.Cell(line, 1).Value = row.Name;
            sheet.Cell(line, 2).Value = row.Description ?? string.Empty;
            sheet.Cell(line, 3).Value = row.PriceAmount;
            sheet.Cell(line, 4).Value = row.Currency;
            sheet.Cell(line, 5).Value = row.DurationMinutes;
            sheet.Cell(line, 6).Value = row.CategorySlug ?? string.Empty;
            sheet.Cell(line, 7).Value = row.IsActive;
            sheet.Cell(line, 8).Value = row.IsAddOn;
            sheet.Cell(line, 9).Value = row.SortOrder;
        }

        sheet.Row(1).Style.Font.Bold = true;
        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public static string BuildAdminBusinessDirectoryCsv(IReadOnlyList<AdminBusinessDirectoryExportRow> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("TenantId,Name,Slug,Status,SubscriptionSku,BillingStatus,TrialEndsAt,CreatedAt");
        foreach (var row in rows)
        {
            builder.Append(row.TenantId);
            builder.Append(',');
            builder.Append(CsvField.Format(row.Name));
            builder.Append(',');
            builder.Append(CsvField.Format(row.Slug));
            builder.Append(',');
            builder.Append(CsvField.Format(row.Status));
            builder.Append(',');
            builder.Append(CsvField.Format(row.SubscriptionSku));
            builder.Append(',');
            builder.Append(CsvField.Format(row.BillingStatus));
            builder.Append(',');
            builder.Append(row.TrialEndsAt?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty);
            builder.Append(',');
            builder.AppendLine(row.CreatedAt.ToString("O", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    public static byte[] BuildAdminBusinessDirectoryXlsx(IReadOnlyList<AdminBusinessDirectoryExportRow> rows) =>
        BuildSimpleSheet(
            "Businesses",
            ["TenantId", "Name", "Slug", "Status", "SubscriptionSku", "BillingStatus", "TrialEndsAt", "CreatedAt"],
            rows,
            (sheet, row, line) =>
            {
                sheet.Cell(line, 1).Value = row.TenantId.ToString();
                sheet.Cell(line, 2).Value = row.Name;
                sheet.Cell(line, 3).Value = row.Slug;
                sheet.Cell(line, 4).Value = row.Status;
                sheet.Cell(line, 5).Value = row.SubscriptionSku;
                sheet.Cell(line, 6).Value = row.BillingStatus;
                sheet.Cell(line, 7).Value = row.TrialEndsAt?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty;
                sheet.Cell(line, 8).Value = row.CreatedAt.ToString("O", CultureInfo.InvariantCulture);
            });

    public static string BuildAdminPendingVerificationsCsv(IReadOnlyList<AdminPendingVerificationExportRow> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("TenantId,Name,Slug,MarketId,SubmittedAt,DocumentCount");
        foreach (var row in rows)
        {
            builder.Append(row.TenantId);
            builder.Append(',');
            builder.Append(CsvField.Format(row.Name));
            builder.Append(',');
            builder.Append(CsvField.Format(row.Slug));
            builder.Append(',');
            builder.Append(CsvField.Format(row.MarketId));
            builder.Append(',');
            builder.Append(row.SubmittedAt.ToString("O", CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.AppendLine(row.DocumentCount.ToString(CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    public static byte[] BuildAdminPendingVerificationsXlsx(IReadOnlyList<AdminPendingVerificationExportRow> rows) =>
        BuildSimpleSheet(
            "Pending",
            ["TenantId", "Name", "Slug", "MarketId", "SubmittedAt", "DocumentCount"],
            rows,
            (sheet, row, line) =>
            {
                sheet.Cell(line, 1).Value = row.TenantId.ToString();
                sheet.Cell(line, 2).Value = row.Name;
                sheet.Cell(line, 3).Value = row.Slug;
                sheet.Cell(line, 4).Value = row.MarketId;
                sheet.Cell(line, 5).Value = row.SubmittedAt.ToString("O", CultureInfo.InvariantCulture);
                sheet.Cell(line, 6).Value = row.DocumentCount;
            });

    public static string BuildAdminTransactionsCsv(IReadOnlyList<AdminTransactionExportRow> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Kind,Id,TenantId,TenantName,ProviderReference,EventType,SubscriptionSku,AmountMinor,Currency,Status,OccurredAt");
        foreach (var row in rows)
        {
            builder.Append(CsvField.Format(row.Kind));
            builder.Append(',');
            builder.Append(row.Id);
            builder.Append(',');
            builder.Append(row.TenantId);
            builder.Append(',');
            builder.Append(CsvField.Format(row.TenantName));
            builder.Append(',');
            builder.Append(CsvField.Format(row.ProviderReference));
            builder.Append(',');
            builder.Append(CsvField.Format(row.EventType));
            builder.Append(',');
            builder.Append(CsvField.Format(row.SubscriptionSku));
            builder.Append(',');
            builder.Append(row.AmountMinor?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
            builder.Append(',');
            builder.Append(CsvField.Format(row.Currency));
            builder.Append(',');
            builder.Append(CsvField.Format(row.Status));
            builder.Append(',');
            builder.AppendLine(row.OccurredAt.ToString("O", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    public static byte[] BuildAdminTransactionsXlsx(IReadOnlyList<AdminTransactionExportRow> rows) =>
        BuildSimpleSheet(
            "Transactions",
            [
                "Kind",
                "Id",
                "TenantId",
                "TenantName",
                "ProviderReference",
                "EventType",
                "SubscriptionSku",
                "AmountMinor",
                "Currency",
                "Status",
                "OccurredAt",
            ],
            rows,
            (sheet, row, line) =>
            {
                sheet.Cell(line, 1).Value = row.Kind;
                sheet.Cell(line, 2).Value = row.Id.ToString();
                sheet.Cell(line, 3).Value = row.TenantId.ToString();
                sheet.Cell(line, 4).Value = row.TenantName;
                sheet.Cell(line, 5).Value = row.ProviderReference ?? string.Empty;
                sheet.Cell(line, 6).Value = row.EventType ?? string.Empty;
                sheet.Cell(line, 7).Value = row.SubscriptionSku ?? string.Empty;
                if (row.AmountMinor is { } minor)
                {
                    sheet.Cell(line, 8).Value = minor;
                }

                sheet.Cell(line, 9).Value = row.Currency;
                sheet.Cell(line, 10).Value = row.Status;
                sheet.Cell(line, 11).Value = row.OccurredAt.ToString("O", CultureInfo.InvariantCulture);
            });

    public static string BuildAdminPricingCatalogEntriesCsv(IReadOnlyList<AdminPricingCatalogEntryExportRow> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("CatalogVersionCode,CountryCode,SubscriptionSku,CurrencyCode,MonthlyAmountMajor,PlatformFeePercent,IsSelfServeEnabled");
        foreach (var row in rows)
        {
            builder.Append(CsvField.Format(row.CatalogVersionCode));
            builder.Append(',');
            builder.Append(CsvField.Format(row.CountryCode));
            builder.Append(',');
            builder.Append(CsvField.Format(row.SubscriptionSku));
            builder.Append(',');
            builder.Append(CsvField.Format(row.CurrencyCode));
            builder.Append(',');
            builder.Append(row.MonthlyAmountMajor?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
            builder.Append(',');
            builder.Append(row.PlatformFeePercent.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.AppendLine(row.IsSelfServeEnabled ? "true" : "false");
        }

        return builder.ToString();
    }

    public static byte[] BuildAdminPricingCatalogEntriesXlsx(IReadOnlyList<AdminPricingCatalogEntryExportRow> rows) =>
        BuildSimpleSheet(
            "Catalog",
            [
                "CatalogVersionCode",
                "CountryCode",
                "SubscriptionSku",
                "CurrencyCode",
                "MonthlyAmountMajor",
                "PlatformFeePercent",
                "IsSelfServeEnabled",
            ],
            rows,
            (sheet, row, line) =>
            {
                sheet.Cell(line, 1).Value = row.CatalogVersionCode;
                sheet.Cell(line, 2).Value = row.CountryCode;
                sheet.Cell(line, 3).Value = row.SubscriptionSku;
                sheet.Cell(line, 4).Value = row.CurrencyCode;
                if (row.MonthlyAmountMajor is { } amount)
                {
                    sheet.Cell(line, 5).Value = amount;
                }

                sheet.Cell(line, 6).Value = row.PlatformFeePercent;
                sheet.Cell(line, 7).Value = row.IsSelfServeEnabled;
            });

    private static byte[] BuildSimpleSheet<T>(
        string sheetName,
        string[] headers,
        IReadOnlyList<T> rows,
        Action<IXLWorksheet, T, int> writeRow)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(sheetName);
        for (var col = 0; col < headers.Length; col++)
        {
            sheet.Cell(1, col + 1).Value = headers[col];
        }

        for (var i = 0; i < rows.Count; i++)
        {
            writeRow(sheet, rows[i]!, i + 2);
        }

        sheet.Row(1).Style.Font.Bold = true;
        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public static byte[] BuildSalesSummaryCsv(SalesSummaryExportRow[] rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Metric,Value,Currency");
        foreach (var row in rows)
        {
            builder.Append(CsvField.Format(row.Metric));
            builder.Append(',');
            builder.Append(CsvField.FormattedNumber(row.Value));
            builder.Append(',');
            builder.AppendLine(CsvField.Format(row.Currency));
        }

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

}
