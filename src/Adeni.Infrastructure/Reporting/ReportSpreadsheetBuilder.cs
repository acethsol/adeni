namespace Adeni.Infrastructure.Reporting;

using System.Globalization;
using System.Text;
using Adeni.Application.Finance;
using Adeni.Application.Reporting;
using ClosedXML.Excel;

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
            builder.Append(Csv(row.BookingId.ToString()));
            builder.Append(',');
            builder.Append(Csv(row.StartAt.ToString("O", CultureInfo.InvariantCulture)));
            builder.Append(',');
            builder.Append(Csv(row.EndAt.ToString("O", CultureInfo.InvariantCulture)));
            builder.Append(',');
            builder.Append(Csv(row.Status));
            builder.Append(',');
            builder.Append(Csv(row.ServiceName));
            builder.Append(',');
            builder.Append(Csv(row.CustomerName));
            builder.Append(',');
            builder.Append(Csv(row.StaffDisplayName));
            builder.Append(',');
            builder.Append(Csv(row.TotalAmount?.ToString(CultureInfo.InvariantCulture)));
            builder.Append(',');
            builder.AppendLine(Csv(row.Currency));
        }

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    public static string BuildStaffRosterCsv(IReadOnlyList<StaffRosterExportRow> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("DisplayName,RoleKey,IsActive,PortalAccess,ServiceCount,SortOrder");
        foreach (var row in rows)
        {
            builder.Append(Csv(row.DisplayName));
            builder.Append(',');
            builder.Append(Csv(row.RoleKey));
            builder.Append(',');
            builder.Append(row.IsActive ? "true" : "false");
            builder.Append(',');
            builder.Append(Csv(row.PortalAccessStatus));
            builder.Append(',');
            builder.Append(row.ServiceCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.AppendLine(row.SortOrder.ToString(CultureInfo.InvariantCulture));
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
            builder.Append(Csv(row.DisplayName));
            builder.Append(',');
            builder.Append(Csv(row.PhoneMasked));
            builder.Append(',');
            builder.Append(Csv(row.Email));
            builder.Append(',');
            builder.Append(Csv(row.ProfileType));
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
            builder.Append(Csv(row.Slug));
            builder.Append(',');
            builder.Append(Csv(row.Name));
            builder.Append(',');
            builder.Append(Csv(row.MarketId));
            builder.Append(',');
            builder.Append(Csv(row.AddressLine));
            builder.Append(',');
            builder.Append(Csv(row.Area));
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
            builder.Append(Csv(row.Name));
            builder.Append(',');
            builder.Append(Csv(row.Description));
            builder.Append(',');
            builder.Append(row.PriceAmount.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(Csv(row.Currency));
            builder.Append(',');
            builder.Append(row.DurationMinutes.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(Csv(row.CategorySlug));
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

    public static byte[] BuildSalesSummaryCsv(SalesSummaryExportRow[] rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Metric,Value,Currency");
        foreach (var row in rows)
        {
            builder.Append(Csv(row.Metric));
            builder.Append(',');
            builder.Append(Csv(row.Value));
            builder.Append(',');
            builder.AppendLine(Csv(row.Currency));
        }

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private static string Csv(string? value)
    {
        var text = value ?? string.Empty;
        if (text.Contains('"') || text.Contains(',') || text.Contains('\n'))
        {
            return $"\"{text.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }

        return text;
    }
}
