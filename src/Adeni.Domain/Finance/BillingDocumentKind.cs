namespace Adeni.Domain.Finance;

public enum BillingDocumentKind
{
    Receipt = 0,
    Invoice = 1,
}

public enum BillingDocumentStatus
{
    Draft = 0,
    Issued = 1,
    Void = 2,
}
