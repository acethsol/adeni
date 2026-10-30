/*
  DEV ONLY - removes everything created by seed-lekki-cuts-demo.sql (matched by its markers).
  Base DevelopmentDataSeeder rows (owner, original staff/customers/bookings/queue, branches) are untouched.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

DECLARE @TenantId UNIQUEIDENTIFIER =
    (SELECT TOP (1) TenantId FROM tenancy.business_locations WHERE Slug = N'lekki-cuts');
IF @TenantId IS NULL THROW 50001, 'Tenant lekki-cuts not found.', 1;

BEGIN TRY
BEGIN TRANSACTION;

DECLARE @Conv TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY);
INSERT @Conv SELECT DISTINCT ConversationId FROM messaging.conversation_messages
WHERE TenantId = @TenantId AND ClientMessageId LIKE N'seed-lekki-demo-%';

DELETE FROM messaging.conversation_messages WHERE ConversationId IN (SELECT Id FROM @Conv);
DELETE FROM messaging.conversation_participants WHERE ConversationId IN (SELECT Id FROM @Conv);
DELETE FROM messaging.conversations WHERE Id IN (SELECT Id FROM @Conv);

DECLARE @Bk TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY);
INSERT @Bk SELECT Id FROM booking.bookings WHERE TenantId = @TenantId AND IdempotencyKey LIKE N'seed-lekki-demo-bk-%';

DELETE FROM booking.reviews WHERE BookingId IN (SELECT Id FROM @Bk);
DELETE FROM booking.queue_tickets WHERE TenantId = @TenantId AND Notes LIKE N'seed-lekki-demo-v1%';
DELETE FROM booking.booking_lines WHERE BookingId IN (SELECT Id FROM @Bk);
DELETE FROM booking.booking_guests WHERE BookingId IN (SELECT Id FROM @Bk);
DELETE FROM booking.bookings WHERE Id IN (SELECT Id FROM @Bk);

DELETE FROM booking.staff_leave WHERE TenantId = @TenantId AND Reason LIKE N'Seed demo:%';
DELETE FROM booking.business_closed_dates WHERE TenantId = @TenantId AND Reason LIKE N'Seed demo:%';

-- staff links / weekly availability / leave cascade from staff_members
DELETE FROM booking.staff_members WHERE TenantId = @TenantId AND Bio = N'seed-lekki-demo-v1';

DELETE FROM tenancy.tenant_customers
WHERE TenantId = @TenantId
  AND (CustomerId IN (SELECT Id FROM [identity].customers WHERE Auth0Sub LIKE N'auth0|dev-lekki-demo-%')
       OR (CustomerId IS NULL AND NormalizedPhone LIKE N'2348032%'));

DELETE FROM [identity].customers WHERE Auth0Sub LIKE N'auth0|dev-lekki-demo-%';

COMMIT TRANSACTION;
PRINT 'Lekki Cuts demo seed removed.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
