namespace Adeni.Infrastructure.Finance;

using System.Security.Cryptography;
using Adeni.Application.Abstractions;
using Adeni.Application.Notifications;
using Adeni.Application.Finance;
using Adeni.Application.Reporting;
using Adeni.Domain.Auditing;
using Adeni.Domain.Booking;
using Adeni.Domain.Common;
using Adeni.Domain.Finance;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class BillingDocumentService(
    AdeniDbContext dbContext,
    IPaymentOrchestrator paymentOrchestrator,
    INotificationDispatcher notificationDispatcher,
    IReportRenderer reportRenderer,
    IAuditLogWriter auditLogWriter,
    ICorrelationContext correlationContext) : IBillingDocumentService
{

    public async Task<Result<BillingDocumentResponse>> GetAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.BillingDocuments
            .AsNoTracking()
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == documentId, cancellationToken);

        return entity is null
            ? Result.Failure<BillingDocumentResponse>(Error.NotFound("Document"))
            : Result.Success(Map(entity));
    }

    public async Task<Result<BillingDocumentResponse>> GetByPublicTokenAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Result.Failure<BillingDocumentResponse>(Error.NotFound("Document"));
        }

        var entity = await dbContext.BillingDocuments
            .AsNoTracking()
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.PublicToken == token.Trim(), cancellationToken);

        if (entity is null || entity.Status == BillingDocumentStatus.Void)
        {
            return Result.Failure<BillingDocumentResponse>(Error.NotFound("Document"));
        }

        return Result.Success(Map(entity));
    }

    public async Task<Result<IReadOnlyList<BillingDocumentResponse>>> ListForTenantAsync(
        Guid tenantId,
        string? kind = null,
        Guid? bookingId = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = dbContext.BillingDocuments
            .AsNoTracking()
            .Include(x => x.Lines)
            .Where(x => x.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(kind)
            && Enum.TryParse<BillingDocumentKind>(kind, ignoreCase: true, out var parsedKind))
        {
            query = query.Where(x => x.Kind == parsedKind);
        }

        if (bookingId is Guid bid)
        {
            query = query.Where(x => x.BookingId == bid);
        }

        var items = await query
            .OrderByDescending(x => x.IssuedAt ?? x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<BillingDocumentResponse>>(items.Select(Map).ToArray());
    }

    public async Task<Result<BillingDocumentResponse>> CreateInvoiceAsync(
        CreateInvoiceRequest request,
        CancellationToken cancellationToken = default)
    {
        var businessName = await ResolveBusinessNameAsync(request.TenantId, cancellationToken);
        var lines = new List<BillingDocumentLine>();
        decimal total;
        string currency;
        string? customerEmail = request.CustomerEmail?.Trim();
        string? customerName = null;
        Guid? bookingId = request.BookingId;

        if (request.BookingId is Guid bid)
        {
            var booking = await dbContext.Bookings
                .AsNoTracking()
                .Include(x => x.Lines)
                .FirstOrDefaultAsync(x => x.Id == bid && x.TenantId == request.TenantId, cancellationToken);

            if (booking is null)
            {
                return Result.Failure<BillingDocumentResponse>(Error.NotFound("Booking"));
            }

            var service = await dbContext.ServiceOfferings
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == booking.ServiceOfferingId, cancellationToken);

            currency = request.Currency?.Trim().ToUpperInvariant()
                ?? service?.Currency
                ?? "NGN";

            if (booking.Lines.Count > 0)
            {
                var order = 0;
                foreach (var line in booking.Lines.OrderBy(x => x.SortOrder))
                {
                    lines.Add(new BillingDocumentLine
                    {
                        Id = Guid.NewGuid(),
                        TenantId = request.TenantId,
                        SortOrder = order++,
                        Description = line.ServiceName,
                        Quantity = 1m,
                        UnitAmount = line.PriceAmount,
                        LineTotal = line.PriceAmount,
                    });
                }

                total = lines.Sum(x => x.LineTotal);
            }
            else
            {
                total = service?.PriceAmount ?? request.Amount ?? 0m;
                lines.Add(new BillingDocumentLine
                {
                    Id = Guid.NewGuid(),
                    TenantId = request.TenantId,
                    SortOrder = 0,
                    Description = request.Description?.Trim() ?? service?.Name ?? "Service",
                    Quantity = 1m,
                    UnitAmount = total,
                    LineTotal = total,
                });
            }

            if (request.Amount is decimal overrideAmount && overrideAmount > 0)
            {
                total = overrideAmount;
                lines =
                [
                    new BillingDocumentLine
                    {
                        Id = Guid.NewGuid(),
                        TenantId = request.TenantId,
                        SortOrder = 0,
                        Description = request.Description?.Trim() ?? "Invoice balance",
                        Quantity = 1m,
                        UnitAmount = total,
                        LineTotal = total,
                    },
                ];
            }

            var customer = await dbContext.Customers
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == booking.CustomerId, cancellationToken);
            customerName = string.IsNullOrWhiteSpace(customer?.Name) ? null : customer.Name;
            customerEmail ??= customer?.Email;
        }
        else
        {
            if (request.Amount is not decimal amount || amount <= 0)
            {
                return Result.Failure<BillingDocumentResponse>(ErrorCodes.PaymentInvalidAmountError());
            }

            total = amount;
            currency = string.IsNullOrWhiteSpace(request.Currency)
                ? "NGN"
                : request.Currency.Trim().ToUpperInvariant();
            lines.Add(new BillingDocumentLine
            {
                Id = Guid.NewGuid(),
                TenantId = request.TenantId,
                SortOrder = 0,
                Description = string.IsNullOrWhiteSpace(request.Description)
                    ? "Invoice"
                    : request.Description.Trim(),
                Quantity = 1m,
                UnitAmount = total,
                LineTotal = total,
            });
        }

        if (total <= 0)
        {
            return Result.Failure<BillingDocumentResponse>(ErrorCodes.PaymentInvalidAmountError());
        }

        Guid? paymentIntentId = null;
        if (request.InitializePayment)
        {
            var payment = await paymentOrchestrator.InitializeAsync(
                new InitializePaymentRequest(
                    request.TenantId,
                    bookingId,
                    total,
                    currency,
                    "invoice",
                    request.Description ?? lines[0].Description,
                    customerEmail,
                    null),
                idempotencyKey: null,
                cancellationToken);

            if (payment.IsFailure)
            {
                return Result.Failure<BillingDocumentResponse>(payment.Error);
            }

            paymentIntentId = payment.Value!.Id;
        }

        var now = DateTimeOffset.UtcNow;
        var document = new BillingDocument
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            Kind = BillingDocumentKind.Invoice,
            Status = request.Issue ? BillingDocumentStatus.Issued : BillingDocumentStatus.Draft,
            DocumentNumber = request.Issue
                ? await NextDocumentNumberAsync(request.TenantId, BillingDocumentKind.Invoice, cancellationToken)
                : $"DRAFT-{Guid.NewGuid():N}"[..16],
            BookingId = bookingId,
            PaymentIntentId = paymentIntentId,
            BusinessNameSnapshot = businessName,
            CustomerNameSnapshot = customerName,
            CustomerEmailSnapshot = customerEmail,
            Currency = currency,
            Subtotal = total,
            TaxTotal = null,
            Total = total,
            MethodLabel = request.InitializePayment ? "Online invoice" : null,
            PublicToken = CreatePublicToken(),
            IssuedAt = request.Issue ? now : null,
            CreatedAt = now,
            UpdatedAt = now,
            Lines = lines.Select(l =>
            {
                l.DocumentId = Guid.Empty;
                return l;
            }).ToList(),
        };

        foreach (var line in document.Lines)
        {
            line.DocumentId = document.Id;
        }

        dbContext.BillingDocuments.Add(document);
        await dbContext.SaveChangesAsync(cancellationToken);

        await WriteAuditAsync(document.Id, document.TenantId, "document.issued", cancellationToken);
        return Result.Success(Map(document));
    }

    public async Task<Result<BillingDocumentResponse>> VoidAsync(
        Guid tenantId,
        Guid documentId,
        VoidBillingDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.BillingDocuments
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == documentId && x.TenantId == tenantId, cancellationToken);

        if (entity is null)
        {
            return Result.Failure<BillingDocumentResponse>(Error.NotFound("Document"));
        }

        if (entity.Status == BillingDocumentStatus.Void)
        {
            return Result.Success(Map(entity));
        }

        entity.Status = BillingDocumentStatus.Void;
        entity.VoidReason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await WriteAuditAsync(entity.Id, entity.TenantId, "document.voided", cancellationToken);
        return Result.Success(Map(entity));
    }

    public async Task<Result<byte[]>> GetPdfAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.BillingDocuments
            .AsNoTracking()
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == documentId, cancellationToken);

        if (entity is null)
        {
            return Result.Failure<byte[]>(Error.NotFound("Document"));
        }

        if (entity.Status == BillingDocumentStatus.Void)
        {
            return Result.Failure<byte[]>(Error.Conflict("Voided documents cannot be downloaded."));
        }

        return await reportRenderer.RenderDocumentAsync(
            ToReportKind(entity.Kind),
            ToReportData(entity),
            cancellationToken);
    }

    public async Task<Result<BillingDocumentResponse>> EmailAsync(
        Guid tenantId,
        Guid documentId,
        EmailBillingDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.BillingDocuments
            .AsNoTracking()
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == documentId && x.TenantId == tenantId, cancellationToken);

        if (entity is null)
        {
            return Result.Failure<BillingDocumentResponse>(Error.NotFound("Document"));
        }

        if (entity.Status != BillingDocumentStatus.Issued)
        {
            return Result.Failure<BillingDocumentResponse>(
                Error.Conflict("Only issued documents can be emailed."));
        }

        var to = string.IsNullOrWhiteSpace(request.ToEmail)
            ? entity.CustomerEmailSnapshot
            : request.ToEmail.Trim();

        if (string.IsNullOrWhiteSpace(to))
        {
            return Result.Failure<BillingDocumentResponse>(
                Error.Validation("A recipient email is required."));
        }

        var mapped = Map(entity);
        var data = new Dictionary<string, string>
        {
            ["kind"] = entity.Kind.ToString(),
            ["kindLower"] = entity.Kind.ToString().ToLowerInvariant(),
            ["documentNumber"] = entity.DocumentNumber,
            ["businessName"] = entity.BusinessNameSnapshot,
            ["total"] = entity.Total.ToString("0.00"),
            ["currency"] = entity.Currency,
            ["publicUrl"] = mapped.PublicUrl ?? string.Empty,
        };

        await notificationDispatcher.SendAsync(
            NotificationMessage.FromTemplate(
                "email",
                to,
                NotificationTemplateIds.BillingDocumentEmailed,
                data,
                entity.TenantId,
                $"billing-document-emailed:{entity.Id:D}"),
            cancellationToken);

        await WriteAuditAsync(entity.Id, entity.TenantId, "document.emailed", cancellationToken);
        return Result.Success(mapped);
    }

    public async Task<Result<BillingDocumentResponse>> GetReceiptForPaymentAsync(
        Guid paymentIntentId,
        CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.BillingDocuments
            .AsNoTracking()
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(
                x => x.PaymentIntentId == paymentIntentId && x.Kind == BillingDocumentKind.Receipt,
                cancellationToken);

        if (existing is not null)
        {
            return Result.Success(Map(existing));
        }

        return await IssueReceiptForPaymentAsync(paymentIntentId, cancellationToken);
    }

    public async Task<Result<BillingDocumentResponse>> IssueReceiptForPaymentAsync(
        Guid paymentIntentId,
        CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.BillingDocuments
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(
                x => x.PaymentIntentId == paymentIntentId && x.Kind == BillingDocumentKind.Receipt,
                cancellationToken);

        if (existing is not null)
        {
            return Result.Success(Map(existing));
        }

        var payment = await dbContext.PaymentIntents
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == paymentIntentId, cancellationToken);

        if (payment is null)
        {
            return Result.Failure<BillingDocumentResponse>(ErrorCodes.PaymentNotFoundError());
        }

        if (payment.Status != PaymentIntentStatus.Completed)
        {
            return Result.Failure<BillingDocumentResponse>(
                Error.Conflict("Receipts are only issued for completed payments."));
        }

        var businessName = await ResolveBusinessNameAsync(payment.TenantId, cancellationToken);
        var (customerName, customerEmail, lines) = await ResolveBookingPartiesAndLinesAsync(
            payment.TenantId,
            payment.BookingId,
            payment.Description,
            payment.Amount,
            payment.CustomerEmail,
            cancellationToken);

        var method = payment.Type switch
        {
            PaymentIntentType.Deposit => "Online deposit",
            PaymentIntentType.Link => "Online payment",
            PaymentIntentType.Invoice => "Online invoice",
            _ => "Online",
        };

        var document = await CreateIssuedDocumentAsync(
            payment.TenantId,
            BillingDocumentKind.Receipt,
            payment.BookingId,
            payment.Id,
            businessName,
            customerName,
            customerEmail ?? payment.CustomerEmail,
            payment.Currency,
            payment.Amount,
            method,
            lines,
            cancellationToken);

        return Result.Success(Map(document));
    }

    public async Task<Result<BillingDocumentResponse>> IssueReceiptForTillPaymentAsync(
        Guid tenantId,
        Guid bookingId,
        string? externalReference = null,
        CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.BillingDocuments
            .Include(x => x.Lines)
            .Where(x =>
                x.TenantId == tenantId
                && x.BookingId == bookingId
                && x.Kind == BillingDocumentKind.Receipt
                && x.PaymentIntentId == null
                && x.Status != BillingDocumentStatus.Void)
            .OrderByDescending(x => x.IssuedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            return Result.Success(Map(existing));
        }

        var booking = await dbContext.Bookings
            .AsNoTracking()
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == bookingId && x.TenantId == tenantId, cancellationToken);

        if (booking is null)
        {
            return Result.Failure<BillingDocumentResponse>(Error.NotFound("Booking"));
        }

        var businessName = await ResolveBusinessNameAsync(tenantId, cancellationToken);
        var service = await dbContext.ServiceOfferings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == booking.ServiceOfferingId, cancellationToken);

        var total = booking.Lines.Count > 0
            ? booking.Lines.Sum(x => x.PriceAmount)
            : service?.PriceAmount ?? 0m;

        var (customerName, customerEmail, lines) = await ResolveBookingPartiesAndLinesAsync(
            tenantId,
            bookingId,
            service?.Name,
            total,
            null,
            cancellationToken);

        var method = string.IsNullOrWhiteSpace(externalReference)
            ? "Paid at till"
            : $"Paid at till ({externalReference.Trim()})";

        var document = await CreateIssuedDocumentAsync(
            tenantId,
            BillingDocumentKind.Receipt,
            bookingId,
            paymentIntentId: null,
            businessName,
            customerName,
            customerEmail,
            service?.Currency ?? "NGN",
            total,
            method,
            lines,
            cancellationToken);

        return Result.Success(Map(document));
    }

    private async Task<BillingDocument> CreateIssuedDocumentAsync(
        Guid tenantId,
        BillingDocumentKind kind,
        Guid? bookingId,
        Guid? paymentIntentId,
        string businessName,
        string? customerName,
        string? customerEmail,
        string currency,
        decimal total,
        string? method,
        IReadOnlyList<(string Description, decimal Quantity, decimal UnitAmount, decimal LineTotal)> lines,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var documentId = Guid.NewGuid();
        var document = new BillingDocument
        {
            Id = documentId,
            TenantId = tenantId,
            Kind = kind,
            Status = BillingDocumentStatus.Issued,
            DocumentNumber = await NextDocumentNumberAsync(tenantId, kind, cancellationToken),
            BookingId = bookingId,
            PaymentIntentId = paymentIntentId,
            BusinessNameSnapshot = businessName,
            CustomerNameSnapshot = customerName,
            CustomerEmailSnapshot = customerEmail,
            Currency = currency,
            Subtotal = total,
            TaxTotal = null,
            Total = total,
            MethodLabel = method,
            PublicToken = CreatePublicToken(),
            IssuedAt = now,
            CreatedAt = now,
            UpdatedAt = now,
            Lines = lines.Select((line, index) => new BillingDocumentLine
            {
                Id = Guid.NewGuid(),
                DocumentId = documentId,
                TenantId = tenantId,
                SortOrder = index,
                Description = line.Description,
                Quantity = line.Quantity,
                UnitAmount = line.UnitAmount,
                LineTotal = line.LineTotal,
            }).ToList(),
        };

        dbContext.BillingDocuments.Add(document);
        await dbContext.SaveChangesAsync(cancellationToken);
        await WriteAuditAsync(document.Id, document.TenantId, "document.issued", cancellationToken);
        return document;
    }

    private async Task<string> NextDocumentNumberAsync(
        Guid tenantId,
        BillingDocumentKind kind,
        CancellationToken cancellationToken)
    {
        var sequence = await dbContext.BillingDocumentSequences
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Kind == kind, cancellationToken);

        if (sequence is null)
        {
            sequence = new BillingDocumentSequence
            {
                TenantId = tenantId,
                Kind = kind,
                NextValue = 1,
            };
            dbContext.BillingDocumentSequences.Add(sequence);
        }

        var value = sequence.NextValue;
        sequence.NextValue = value + 1;
        var prefix = kind == BillingDocumentKind.Receipt ? "RCP" : "INV";
        var year = DateTimeOffset.UtcNow.Year;
        return $"{prefix}-{year}-{value:D5}";
    }

    private async Task<string> ResolveBusinessNameAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await dbContext.Tenants
            .AsNoTracking()
            .Where(x => x.Id == tenantId)
            .Select(x => x.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "Business";

    private async Task<(string? Name, string? Email, IReadOnlyList<(string, decimal, decimal, decimal)> Lines)>
        ResolveBookingPartiesAndLinesAsync(
            Guid tenantId,
            Guid? bookingId,
            string? fallbackDescription,
            decimal fallbackAmount,
            string? fallbackEmail,
            CancellationToken cancellationToken)
    {
        if (bookingId is not Guid bid)
        {
            return (
                null,
                fallbackEmail,
                [(fallbackDescription ?? "Payment", 1m, fallbackAmount, fallbackAmount)]);
        }

        var booking = await dbContext.Bookings
            .AsNoTracking()
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == bid && x.TenantId == tenantId, cancellationToken);

        if (booking is null)
        {
            return (
                null,
                fallbackEmail,
                [(fallbackDescription ?? "Payment", 1m, fallbackAmount, fallbackAmount)]);
        }

        var customer = await dbContext.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == booking.CustomerId, cancellationToken);

        IReadOnlyList<(string, decimal, decimal, decimal)> lines;
        if (booking.Lines.Count > 0)
        {
            lines = booking.Lines
                .OrderBy(x => x.SortOrder)
                .Select(x => (x.ServiceName, 1m, x.PriceAmount, x.PriceAmount))
                .ToArray();
        }
        else
        {
            var serviceName = await dbContext.ServiceOfferings
                .AsNoTracking()
                .Where(x => x.Id == booking.ServiceOfferingId)
                .Select(x => x.Name)
                .FirstOrDefaultAsync(cancellationToken);
            lines = [(serviceName ?? fallbackDescription ?? "Service", 1m, fallbackAmount, fallbackAmount)];
        }

        var name = string.IsNullOrWhiteSpace(customer?.Name) ? null : customer.Name;
        return (name, customer?.Email ?? fallbackEmail, lines);
    }

    private BillingDocumentResponse Map(BillingDocument entity)
    {
        // Relative discover path; portals/apps prefix with their public web origin.
        var publicUrl = $"/documents/{entity.PublicToken}";

        return new BillingDocumentResponse(
            entity.Id,
            entity.TenantId,
            entity.Kind.ToString().ToLowerInvariant(),
            entity.Status.ToString().ToLowerInvariant(),
            entity.DocumentNumber,
            entity.BookingId,
            entity.PaymentIntentId,
            entity.BusinessNameSnapshot,
            entity.CustomerNameSnapshot,
            entity.CustomerEmailSnapshot,
            entity.Currency,
            entity.Subtotal,
            entity.TaxTotal,
            entity.Total,
            entity.MethodLabel,
            entity.IssuedAt,
            entity.Lines
                .OrderBy(x => x.SortOrder)
                .Select(x => new BillingDocumentLineResponse(
                    x.Description,
                    x.Quantity,
                    x.UnitAmount,
                    x.LineTotal,
                    x.SortOrder))
                .ToArray(),
            publicUrl,
            entity.PublicToken);
    }

    private static string CreatePublicToken()
    {
        Span<byte> bytes = stackalloc byte[24];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static ReportKind ToReportKind(BillingDocumentKind kind) =>
        kind == BillingDocumentKind.Receipt ? ReportKind.BillingReceipt : ReportKind.BillingInvoice;

    private static BillingDocumentReportData ToReportData(BillingDocument entity) =>
        new(
            ToReportKind(entity.Kind),
            entity.DocumentNumber,
            entity.BusinessNameSnapshot,
            entity.CustomerNameSnapshot,
            entity.Currency,
            entity.Total,
            entity.MethodLabel,
            entity.IssuedAt,
            entity.Lines
                .OrderBy(x => x.SortOrder)
                .Select(x => new BillingDocumentLineReportData(
                    x.Description,
                    x.Quantity,
                    x.UnitAmount,
                    x.LineTotal,
                    x.SortOrder))
                .ToArray());

    private async Task WriteAuditAsync(
        Guid documentId,
        Guid tenantId,
        string action,
        CancellationToken cancellationToken) =>
        await auditLogWriter.WriteAsync(
            new AuditEntry(
                Guid.NewGuid(),
                tenantId.ToString(),
                action,
                "billing_document",
                documentId.ToString(),
                correlationContext.CorrelationId,
                DateTimeOffset.UtcNow),
            cancellationToken);
}
