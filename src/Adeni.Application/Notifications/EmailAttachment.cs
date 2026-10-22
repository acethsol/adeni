namespace Adeni.Application.Notifications;

public sealed record EmailAttachment(
    string FileName,
    string ContentType,
    byte[] Content);
