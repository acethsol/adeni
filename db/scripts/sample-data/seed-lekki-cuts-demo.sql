/*
  DEV ONLY - sizeable demo data for Lekki Cuts (tenant slug "lekki-cuts"), every active branch.
  Makes portal/admin grids testable: staff, customers, bookings, reviews, messages, queue, calendar blocks.

  Idempotent: each block skips when its own marker already exists. Safe to re-run.
  Undo:       db/scripts/seed-lekki-cuts-demo-cleanup.sql
  Run:        scripts/apply-lekki-cuts-demo-seed.ps1   (or sqlcmd -I -i this file; see docs/database-setup.md)

  Markers (all fake PII, no real contact data):
    staff_members.Bio                    = 'seed-lekki-demo-v1'
    identity.customers.Auth0Sub          = 'auth0|dev-lekki-demo-NNN'   (email NNN@seed.lekki.local)
    bookings.IdempotencyKey              = 'seed-lekki-demo-bk-...'
    queue_tickets.Notes                  = 'seed-lekki-demo-v1 ...'
    conversation_messages.ClientMessageId= 'seed-lekki-demo-...'
    staff_leave / business_closed_dates  Reason starts with 'Seed demo:'

  Tenant isolation: every row carries the Lekki Cuts TenantId; nothing is written for other tenants.
  Requires the DevelopmentDataSeeder base data (tenant, branches, services, auth0|local-business owner).
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;   -- required by filtered indexes (sqlcmd defaults to OFF)
SET ANSI_NULLS ON;

DECLARE @TenantSlug  NVARCHAR(64) = N'lekki-cuts';
DECLARE @Marker      NVARCHAR(64) = N'seed-lekki-demo-v1';
DECLARE @StaffTarget INT          = 30;     -- active staff per branch (existing + seeded)
DECLARE @Now         DATETIMEOFFSET(7) = SYSDATETIMEOFFSET();
DECLARE @TodayLocal  DATE = CAST(SWITCHOFFSET(@Now, '+01:00') AS DATE);  -- Africa/Lagos (UTC+1, no DST)

DECLARE @TenantId UNIQUEIDENTIFIER =
    (SELECT TOP (1) TenantId FROM tenancy.business_locations WHERE Slug = @TenantSlug);
IF @TenantId IS NULL
    THROW 50001, 'Tenant for slug lekki-cuts not found. Start the API in Development once so DevelopmentDataSeeder runs.', 1;

DECLARE @TenantStatus INT = (SELECT [Status] FROM tenancy.tenants WHERE Id = @TenantId);
IF @TenantStatus <> 2
    PRINT 'WARNING: tenant is not Verified (Status=2) - it will not appear in discover.';

DECLARE @OwnerBusinessUserId UNIQUEIDENTIFIER, @OwnerAuth0Sub NVARCHAR(128);
SELECT TOP (1) @OwnerBusinessUserId = Id, @OwnerAuth0Sub = Auth0Sub
FROM [identity].business_users
WHERE TenantId = @TenantId
ORDER BY CASE WHEN Auth0Sub = N'auth0|local-business' THEN 0 ELSE 1 END, CreatedAt;
IF @OwnerBusinessUserId IS NULL
    THROW 50002, 'No business owner user for Lekki Cuts (auth0|local-business). Run the API once in Development.', 1;

------------------------------------------------------------------------------
-- Lookup / helper temp tables
------------------------------------------------------------------------------
CREATE TABLE #Loc (Seq INT NOT NULL PRIMARY KEY, LocId UNIQUEIDENTIFIER NOT NULL, Slug NVARCHAR(64), Name NVARCHAR(200));
INSERT #Loc (Seq, LocId, Slug, Name)
SELECT ROW_NUMBER() OVER (ORDER BY CASE WHEN Slug = @TenantSlug THEN 0 ELSE 1 END, CreatedAt),
       Id, Slug, Name
FROM tenancy.business_locations
WHERE TenantId = @TenantId AND IsActive = 1;

DECLARE @LocCount INT = (SELECT COUNT(*) FROM #Loc);
IF @LocCount = 0 THROW 50003, 'No active branches for Lekki Cuts.', 1;
DECLARE @CustTotal INT = 50 * @LocCount + 20;   -- branch pools of 70 customers, 20 overlap between neighbours

CREATE TABLE #N (n INT NOT NULL PRIMARY KEY);
INSERT #N (n)
SELECT TOP (3000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) FROM sys.all_objects a CROSS JOIN sys.all_objects b;

CREATE TABLE #FN (i INT NOT NULL PRIMARY KEY, v NVARCHAR(60) NOT NULL);
INSERT #FN (i, v)
SELECT CAST([key] AS INT), [value] FROM OPENJSON(N'["Adaeze","Tunde","Chioma","Emeka","Fatima","Segun","Ngozi","Ibrahim","Folake","Chidi","Aisha","Kunle","Amaka","Yusuf","Bisi","Obinna","Zainab","Damilola","Ifeoma","Olumide","Hauwa","Tobi","Nkechi","Femi","Halima","Uche","Bukola","Musa","Sade","Ikenna","Temitope","Jide","Blessing","Gbenga","Funmi","Chinedu","Maryam","Lanre","Efe","Seyi","Omolara","Kelechi","Rotimi","Yetunde","Dayo","Amina","Tochi","Wale","Ronke","Babatunde"]');

CREATE TABLE #LN (i INT NOT NULL PRIMARY KEY, v NVARCHAR(60) NOT NULL);
INSERT #LN (i, v)
SELECT CAST([key] AS INT), [value] FROM OPENJSON(N'["Okafor","Balogun","Adeyemi","Nwosu","Bello","Ogunleye","Eze","Abubakar","Adebayo","Okonkwo","Lawal","Ibe","Oladipo","Musa","Akinwale","Umeh","Salako","Obi","Danjuma","Fashola","Anyanwu","Ogundipe","Mohammed","Chukwu","Alabi","Onyeama","Sanni","Okoye","Bankole","Idowu","Nnamdi","Olawale","Yakubu","Eweka","Ajayi","Okeke","Shittu","Adewale","Nwachukwu","Ogunbanjo","Ekwueme","Tijani","Afolabi","Ojo","Uzor","Coker","Aluko","Dosunmu","Anigbogu","Odukoya"]');

CREATE TABLE #RolePat (i INT NOT NULL PRIMARY KEY, RoleKey NVARCHAR(40) NOT NULL);
INSERT #RolePat (i, RoleKey)
SELECT CAST([key] AS INT), [value] FROM OPENJSON(N'["barber","stylist","barber","stylist","barber","stylist","barber","stylist","barber","stylist","barber","stylist","barber","stylist","barber","stylist","barber","stylist","barber","stylist","barber","receptionist","receptionist","receptionist","supervisor","supervisor","marketing","hr","admin_staff","inventory_manager"]');

CREATE TABLE #Txt (Kind VARCHAR(16) NOT NULL, i INT NOT NULL, Txt NVARCHAR(1000) NOT NULL, PRIMARY KEY (Kind, i));
INSERT #Txt (Kind, i, Txt)
SELECT 'cmsg', CAST([key] AS INT), [value] FROM OPENJSON(N'["Hi, can I move my appointment to a later time today? I am stuck in traffic on the Lekki-Epe expressway.","Good afternoon, please confirm my booking and let me know if I should come with my hair washed.","Hello, is it possible to add a beard trim to my booking?","Running about 15 minutes late, please do not give away my slot.","Hi, can I switch to a different barber for this booking? I preferred the one I saw last time.","Do you take transfers or only card at the shop?","Please can I get a reminder of the exact address and where to park?","Hello, I need to reschedule to next week. What days do you have free?","Is there a deposit for braids? I want to be sure before I come.","Thanks for the great service last time, booking again for my brother too."]');
INSERT #Txt (Kind, i, Txt)
SELECT 'omsg', CAST([key] AS INT), [value] FROM OPENJSON(N'["Hello! Yes, we can shift you by 30 minutes. See you soon.","Your booking is confirmed. Please come with clean, dry hair.","Yes, we can add a beard trim. It adds about 20 minutes.","No problem, we will hold your slot for 15 minutes.","We will try to match you with your preferred stylist. See you then.","We accept card, bank transfer and cash at the front desk.","We are at the Admiralty Way plaza, parking is behind the building.","We have openings Tuesday to Friday next week. Tell me a time that works."]');
INSERT #Txt (Kind, i, Txt)
SELECT 'cfup', CAST([key] AS INT), [value] FROM OPENJSON(N'["Thank you, that works for me.","Great, I am on my way now.","Okay, please reserve it. I will pay at the shop.","Can you confirm the price before I arrive?","Perfect, see you then.","Sorry, one more thing - can my friend join me?"]');
INSERT #Txt (Kind, i, Txt)
SELECT 'pmsg', CAST([key] AS INT), [value] FROM OPENJSON(N'["Hi - what time do you open on Saturday?","Do you do kids haircuts?","Do you offer home service in Lekki?","Is Baba Fela available this weekend?","What products do you use for locs maintenance?","Do you offer student discounts?","Can I book a group of four for a wedding party?","Do you have a female barber available?","What is the cancellation policy?","Do you sell beard oil at the shop?"]');
INSERT #Txt (Kind, i, Txt)
SELECT 'rv5', CAST([key] AS INT), [value] FROM OPENJSON(N'["Excellent fade, exactly what I asked for. Will be back.","Clean shop, friendly team and very professional.","Best braids I have had in Lagos. Neat and fast.","Booked online and was seen on time. Highly recommend.","My stylist was patient and explained everything. Great colour result.","Fantastic beard grooming, sharp lines and a warm towel finish.","Lovely experience from start to finish.","Worth every naira. My locs look amazing."]');
INSERT #Txt (Kind, i, Txt)
SELECT 'rv4', CAST([key] AS INT), [value] FROM OPENJSON(N'["Great cut, only waited about 10 minutes past my slot.","Good service and fair price. Parking is a bit tight.","Very good result, would have liked a quicker finish.","Nice stylist and clean space. Booking process was easy.","Solid service overall.","Happy with the haircut, will book again."]');
INSERT #Txt (Kind, i, Txt)
SELECT 'rv3', CAST([key] AS INT), [value] FROM OPENJSON(N'["Decent service but the wait was longer than expected.","Haircut was okay, not quite what I showed in the photo.","Friendly staff but the appointment started late.","Average experience."]');
INSERT #Txt (Kind, i, Txt)
SELECT 'rv2', CAST([key] AS INT), [value] FROM OPENJSON(N'["Waited almost 40 minutes past my booking time.","Style was uneven on one side, had to get it fixed.","Not impressed with the service this time."]');
INSERT #Txt (Kind, i, Txt)
SELECT 'rv1', CAST([key] AS INT), [value] FROM OPENJSON(N'["Booking was not honoured on time and nobody updated me.","Very disappointed, the colour did not match what we agreed.","Poor front desk experience."]');
INSERT #Txt (Kind, i, Txt)
SELECT 'rpos', CAST([key] AS INT), [value] FROM OPENJSON(N'["Thank you for the kind words! See you next time.","We appreciate you, thanks for visiting Lekki Cuts.","Glad you loved it - the team will be happy to hear this."]');
INSERT #Txt (Kind, i, Txt)
SELECT 'rneg', CAST([key] AS INT), [value] FROM OPENJSON(N'["We are sorry to hear this. Please contact us so we can make it right.","Thank you for the feedback. We are reviewing our scheduling to avoid delays."]');
INSERT #Txt (Kind, i, Txt)
SELECT 'bnote', CAST([key] AS INT), [value] FROM OPENJSON(N'["Regular client, prefers low fade.","Asked for extra edge-up.","Paid cash at till.","Referred by Instagram.","Sensitive scalp - use gentle products."]');
INSERT #Txt (Kind, i, Txt)
SELECT 'cnote', CAST([key] AS INT), [value] FROM OPENJSON(N'["First visit, please be gentle.","Bringing a reference photo.","Prefer a quiet chair if possible.","Will arrive 5 minutes early.","Allergic to strong fragrances."]');

CREATE TABLE #TxtCnt (Kind VARCHAR(16) PRIMARY KEY, Cnt INT NOT NULL);
INSERT #TxtCnt SELECT Kind, COUNT(*) FROM #Txt GROUP BY Kind;

-- Services: practitioner role -> bookable service pool
CREATE TABLE #RoleSvc (RoleKey NVARCHAR(40) NOT NULL, SvcId UNIQUEIDENTIFIER NOT NULL, Name NVARCHAR(200), Price DECIMAL(12,2), Dur INT, rn INT NOT NULL, PRIMARY KEY (RoleKey, SvcId));
;WITH base AS (
    SELECT so.Id, so.Name, so.PriceAmount, so.DurationMinutes
    FROM booking.service_offerings so
    WHERE so.TenantId = @TenantId AND so.IsActive = 1 AND so.IsAddOn = 0 AND so.DurationMinutes <= 120
), pairs AS (
    SELECT r.RoleKey, b.Id, b.Name, b.PriceAmount, b.DurationMinutes
    FROM (VALUES (N'barber', N'Barbering'), (N'barber', N'Haircut'), (N'barber', N'Beard Grooming'),
                 (N'stylist', N'Hair Styling'), (N'stylist', N'Hair Coloring'), (N'stylist', N'Locs'),
                 (N'stylist', N'Braids'), (N'stylist', N'Hair Treatment'), (N'stylist', N'Haircut')) r(RoleKey, SvcName)
    JOIN base b ON b.Name = r.SvcName
)
INSERT #RoleSvc (RoleKey, SvcId, Name, Price, Dur, rn)
SELECT RoleKey, Id, Name, PriceAmount, DurationMinutes, ROW_NUMBER() OVER (PARTITION BY RoleKey ORDER BY Name)
FROM pairs;
-- Fallback for renamed catalogs: any role without matches gets every active service
INSERT #RoleSvc (RoleKey, SvcId, Name, Price, Dur, rn)
SELECT r.RoleKey, b.Id, b.Name, b.PriceAmount, b.DurationMinutes, ROW_NUMBER() OVER (PARTITION BY r.RoleKey ORDER BY b.Name)
FROM (VALUES (N'barber'), (N'stylist')) r(RoleKey)
CROSS JOIN (SELECT so.Id, so.Name, so.PriceAmount, so.DurationMinutes FROM booking.service_offerings so
            WHERE so.TenantId = @TenantId AND so.IsActive = 1 AND so.IsAddOn = 0 AND so.DurationMinutes <= 120) b
WHERE NOT EXISTS (SELECT 1 FROM #RoleSvc x WHERE x.RoleKey = r.RoleKey);
IF NOT EXISTS (SELECT 1 FROM #RoleSvc) THROW 50004, 'No active non-add-on services (<=120 min) for Lekki Cuts.', 1;
CREATE TABLE #RoleSvcCnt (RoleKey NVARCHAR(40) PRIMARY KEY, Cnt INT NOT NULL);
INSERT #RoleSvcCnt SELECT RoleKey, COUNT(*) FROM #RoleSvc GROUP BY RoleKey;

BEGIN TRY
BEGIN TRANSACTION;

------------------------------------------------------------------------------
-- 1. Staff: top each active branch up to @StaffTarget active members (+1 inactive)
------------------------------------------------------------------------------
INSERT booking.staff_members
    (Id, TenantId, FirstName, LastName, DisplayName, RoleKey, Title, Bio, IsActive, SortOrder,
     BusinessLocationId, AvatarImageKey, CreatedAt, UpdatedAt)
SELECT NEWID(), @TenantId, fn.v, ln.v, CONCAT(fn.v, N' ', LEFT(ln.v, 1), N'.'), rp.RoleKey,
       CASE rp.RoleKey
            WHEN N'barber' THEN IIF(num.n % 4 = 0, N'Senior Barber', N'Barber')
            WHEN N'stylist' THEN IIF(num.n % 5 = 0, N'Colour Specialist', N'Stylist')
            WHEN N'receptionist' THEN N'Front Desk'
            WHEN N'supervisor' THEN N'Floor Supervisor'
            WHEN N'marketing' THEN N'Marketing Lead'
            WHEN N'hr' THEN N'HR Officer'
            WHEN N'admin_staff' THEN N'Admin and Ops'
            WHEN N'inventory_manager' THEN N'Inventory Manager'
            ELSE N'Team' END,
       @Marker,
       IIF(num.n = 14, 0, 1),
       100 + num.n,
       L.LocId, NULL, DATEADD(day, -(30 + num.n * 3), @Now), @Now
FROM #Loc L
CROSS APPLY (SELECT COUNT(*) AS cnt FROM booking.staff_members s
             WHERE s.TenantId = @TenantId AND s.BusinessLocationId = L.LocId AND s.IsActive = 1) ex
JOIN #N num ON num.n <= CASE WHEN @StaffTarget > ex.cnt THEN @StaffTarget - ex.cnt + 1 ELSE 0 END
CROSS APPLY (SELECT (L.Seq - 1) * 31 + num.n AS k) kk
JOIN #FN fn ON fn.i = kk.k % 50
JOIN #LN ln ON ln.i = (kk.k * 7 + kk.k / 50) % 50
JOIN #RolePat rp ON rp.i = (num.n - 1) % 30
WHERE NOT EXISTS (SELECT 1 FROM booking.staff_members s
                  WHERE s.TenantId = @TenantId AND s.BusinessLocationId = L.LocId AND s.Bio = @Marker);
PRINT CONCAT('staff_members inserted: ', @@ROWCOUNT);

-- Seeded active practitioners (bookable) per branch
CREATE TABLE #Prac (LocSeq INT NOT NULL, LocId UNIQUEIDENTIFIER NOT NULL, StaffId UNIQUEIDENTIFIER NOT NULL, Seq INT NOT NULL, RoleKey NVARCHAR(40) NOT NULL, PRIMARY KEY (LocSeq, Seq));
INSERT #Prac (LocSeq, LocId, StaffId, Seq, RoleKey)
SELECT L.Seq, L.LocId, s.Id, ROW_NUMBER() OVER (PARTITION BY L.Seq ORDER BY s.SortOrder, s.LastName, s.FirstName), s.RoleKey
FROM booking.staff_members s
JOIN #Loc L ON L.LocId = s.BusinessLocationId
WHERE s.TenantId = @TenantId AND s.Bio = @Marker AND s.IsActive = 1 AND s.RoleKey IN (N'barber', N'stylist');

INSERT booking.staff_service_links (StaffMemberId, ServiceOfferingId, TenantId)
SELECT p.StaffId, rs.SvcId, @TenantId
FROM #Prac p JOIN #RoleSvc rs ON rs.RoleKey = p.RoleKey
WHERE NOT EXISTS (SELECT 1 FROM booking.staff_service_links x WHERE x.StaffMemberId = p.StaffId AND x.ServiceOfferingId = rs.SvcId);
PRINT CONCAT('staff_service_links inserted: ', @@ROWCOUNT);

-- Weekly rota: Mon-Fri 09-17, Sat 10-16 (bookings use 10:00-16:30). Every 3rd practitioner has one weekday off.
INSERT booking.staff_weekly_availability (Id, TenantId, StaffMemberId, DayOfWeek, OpenTime, CloseTime)
SELECT NEWID(), @TenantId, p.StaffId, dw.d,
       IIF(dw.d = 6, '10:00', '09:00'), IIF(dw.d = 6, '16:00', '17:00')
FROM #Prac p
CROSS JOIN (VALUES (1), (2), (3), (4), (5), (6)) dw(d)
WHERE NOT (p.Seq % 3 = 0 AND dw.d = (p.Seq % 6) + 1)
  AND NOT EXISTS (SELECT 1 FROM booking.staff_weekly_availability a WHERE a.StaffMemberId = p.StaffId);
PRINT CONCAT('staff_weekly_availability inserted: ', @@ROWCOUNT);

------------------------------------------------------------------------------
-- 2. Calendar: staff leave + branch closed dates
------------------------------------------------------------------------------
INSERT booking.staff_leave (Id, TenantId, StaffMemberId, StartAt, EndAt, Reason, CreatedAt)
SELECT NEWID(), @TenantId, p.StaffId,
       TODATETIMEOFFSET(CAST(DATEADD(day, lv.sd, @TodayLocal) AS DATETIME2(0)), '+01:00'),
       TODATETIMEOFFSET(CAST(DATEADD(day, lv.ed + 1, @TodayLocal) AS DATETIME2(0)), '+01:00'),
       lv.Reason, DATEADD(day, -10, @Now)
FROM #Prac p
JOIN (VALUES (3, -6, -4, N'Seed demo: Annual leave'),
             (8, -1,  2, N'Seed demo: Sick leave'),
             (13, 6,  9, N'Seed demo: Family event'),
             (18, 20, 27, N'Seed demo: Annual leave')) lv(Seq, sd, ed, Reason) ON lv.Seq = p.Seq
WHERE NOT EXISTS (SELECT 1 FROM booking.staff_leave x
                  WHERE x.TenantId = @TenantId AND x.StaffMemberId = p.StaffId AND x.Reason LIKE N'Seed demo:%');
PRINT CONCAT('staff_leave inserted: ', @@ROWCOUNT);

INSERT booking.business_closed_dates (Id, TenantId, BusinessLocationId, StartDate, EndDate, Reason, CreatedAt)
SELECT NEWID(), @TenantId, L.LocId, cd.sd, cd.ed, cd.Reason, @Now
FROM #Loc L
CROSS JOIN (VALUES
    (DATEFROMPARTS(YEAR(@TodayLocal), 10, 1), DATEFROMPARTS(YEAR(@TodayLocal), 10, 1), N'Seed demo: Independence Day (public holiday)'),
    (DATEADD(day, 10, @TodayLocal), DATEADD(day, 10, @TodayLocal), N'Seed demo: Staff training day'),
    (IIF(@TodayLocal > DATEFROMPARTS(YEAR(@TodayLocal), 12, 26), DATEFROMPARTS(YEAR(@TodayLocal) + 1, 12, 25), DATEFROMPARTS(YEAR(@TodayLocal), 12, 25)),
     IIF(@TodayLocal > DATEFROMPARTS(YEAR(@TodayLocal), 12, 26), DATEFROMPARTS(YEAR(@TodayLocal) + 1, 12, 26), DATEFROMPARTS(YEAR(@TodayLocal), 12, 26)),
     N'Seed demo: Christmas and Boxing Day')) cd(sd, ed, Reason)
WHERE NOT EXISTS (SELECT 1 FROM booking.business_closed_dates x
                  WHERE x.TenantId = @TenantId AND x.BusinessLocationId = L.LocId AND x.Reason = cd.Reason);
PRINT CONCAT('business_closed_dates inserted: ', @@ROWCOUNT);

------------------------------------------------------------------------------
-- 3. Customers (platform profiles + tenant CRM rows) and walk-in CRM rows
------------------------------------------------------------------------------
INSERT [identity].customers (Id, Auth0Sub, Name, Email, Phone, PreferredLocale, CreatedAt)
SELECT NEWID(), CONCAT(N'auth0|dev-lekki-demo-', RIGHT(CONCAT('000', n.n), 3)),
       CONCAT(fn.v, N' ', ln.v),
       LOWER(CONCAT(fn.v, N'.', ln.v, N'.', RIGHT(CONCAT('000', n.n), 3), N'@seed.lekki.local')),
       CONCAT(N'+2348031', RIGHT(CONCAT('000000', n.n), 6)),
       N'en',
       DATEADD(day, -(15 + ABS(CAST(CHECKSUM(CONCAT('cc', n.n)) AS BIGINT)) % 180), @Now)
FROM #N n
JOIN #FN fn ON fn.i = (n.n * 3) % 50
JOIN #LN ln ON ln.i = (n.n * 7 + n.n / 50) % 50
WHERE n.n <= @CustTotal
  AND NOT EXISTS (SELECT 1 FROM [identity].customers c
                  WHERE c.Auth0Sub = CONCAT(N'auth0|dev-lekki-demo-', RIGHT(CONCAT('000', n.n), 3)));
PRINT CONCAT('identity.customers inserted: ', @@ROWCOUNT);

INSERT tenancy.tenant_customers
    (Id, TenantId, CustomerId, NormalizedPhone, DisplayName, FirstName, LastName, Email, CreatedAt, UpdatedAt, LastSeenAt)
SELECT NEWID(), @TenantId, c.Id, REPLACE(c.Phone, N'+', N''), c.Name, fn.v, ln.v, c.Email, c.CreatedAt, c.CreatedAt, c.CreatedAt
FROM [identity].customers c
CROSS APPLY (SELECT CAST(RIGHT(c.Auth0Sub, 3) AS INT) AS n) x
JOIN #FN fn ON fn.i = (x.n * 3) % 50
JOIN #LN ln ON ln.i = (x.n * 7 + x.n / 50) % 50
WHERE c.Auth0Sub LIKE N'auth0|dev-lekki-demo-%'
  AND NOT EXISTS (SELECT 1 FROM tenancy.tenant_customers t WHERE t.TenantId = @TenantId AND t.CustomerId = c.Id)
  AND NOT EXISTS (SELECT 1 FROM tenancy.tenant_customers t WHERE t.TenantId = @TenantId AND t.NormalizedPhone = REPLACE(c.Phone, N'+', N''));
PRINT CONCAT('tenant_customers (linked) inserted: ', @@ROWCOUNT);

INSERT tenancy.tenant_customers
    (Id, TenantId, CustomerId, NormalizedPhone, DisplayName, FirstName, LastName, Email, CreatedAt, UpdatedAt, LastSeenAt)
SELECT NEWID(), @TenantId, NULL, CONCAT(N'2348032', RIGHT(CONCAT('000000', n.n), 6)),
       CONCAT(fn.v, N' ', ln.v), fn.v, ln.v,
       IIF(n.n % 3 = 0, NULL, LOWER(CONCAT(fn.v, N'.', ln.v, N'.w', RIGHT(CONCAT('00', n.n), 2), N'@seed.lekki.local'))),
       DATEADD(day, -(5 + n.n * 4), @Now), DATEADD(day, -(5 + n.n * 4), @Now), DATEADD(day, -(1 + n.n), @Now)
FROM #N n
JOIN #FN fn ON fn.i = (n.n * 9 + 1) % 50
JOIN #LN ln ON ln.i = (n.n * 11 + 5) % 50
WHERE n.n <= 20
  AND NOT EXISTS (SELECT 1 FROM tenancy.tenant_customers t
                  WHERE t.TenantId = @TenantId AND t.NormalizedPhone = CONCAT(N'2348032', RIGHT(CONCAT('000000', n.n), 6)));
PRINT CONCAT('tenant_customers (walk-in) inserted: ', @@ROWCOUNT);

CREATE TABLE #Cust (n INT NOT NULL PRIMARY KEY, CustomerId UNIQUEIDENTIFIER NOT NULL, TenantCustomerId UNIQUEIDENTIFIER NULL);
INSERT #Cust (n, CustomerId, TenantCustomerId)
SELECT CAST(RIGHT(c.Auth0Sub, 3) AS INT), c.Id, t.Id
FROM [identity].customers c
LEFT JOIN tenancy.tenant_customers t ON t.TenantId = @TenantId AND t.CustomerId = c.Id
WHERE c.Auth0Sub LIKE N'auth0|dev-lekki-demo-%';

------------------------------------------------------------------------------
-- 4. Bookings (+ lines): past / today / upcoming, no staff double-booking
------------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM booking.bookings WHERE TenantId = @TenantId AND IdempotencyKey LIKE N'seed-lekki-demo-bk-%')
BEGIN
    ;WITH Days AS (SELECT n.n - 36 AS d FROM #N n WHERE n.n <= 50),          -- -35 .. +14
    Raw AS (
        SELECT p.LocSeq, p.LocId, p.StaffId, p.Seq AS pSeq, p.RoleKey, dd.d, sl.s,
               CAST(DATEADD(day, dd.d, @TodayLocal) AS DATE) AS DayLocal,
               CONCAT(p.LocSeq, N'-', p.Seq, N'-', dd.d, N'-', sl.s) AS k
        FROM #Prac p CROSS JOIN Days dd CROSS JOIN (VALUES (0), (1), (2)) sl(s)
    ),
    H AS (
        SELECT r.*,
               DATEDIFF(day, '19000101', r.DayLocal) % 7 AS wd,            -- 0=Mon .. 6=Sun
               ABS(CAST(CHECKSUM(CONCAT(r.k, N'a')) AS BIGINT)) % 1000 AS h1,
               ABS(CAST(CHECKSUM(CONCAT(r.k, N'b')) AS BIGINT)) AS h2,
               ABS(CAST(CHECKSUM(CONCAT(r.k, N'c')) AS BIGINT)) AS h3,
               ABS(CAST(CHECKSUM(CONCAT(r.k, N'd')) AS BIGINT)) % 100 AS h4,
               ABS(CAST(CHECKSUM(CONCAT(r.k, N'e')) AS BIGINT)) AS h5,
               ABS(CAST(CHECKSUM(CONCAT(r.k, N'f')) AS BIGINT)) AS h6,
               ABS(CAST(CHECKSUM(CONCAT(r.k, N'g')) AS BIGINT)) AS h7
        FROM Raw r
    ),
    Pick AS (
        SELECT h.*, rs.SvcId, rs.Name AS SvcName, rs.Price, rs.Dur,
               TODATETIMEOFFSET(DATEADD(minute, (10 + 2 * h.s) * 60 + IIF(h.h2 % 2 = 0 OR rs.Dur > 90, 0, 30),   -- half-hour starts only when they still fit the 2h slot
                                        CAST(h.DayLocal AS DATETIME2(0))), '+01:00') AS StartAt
        FROM H h
        JOIN #RoleSvcCnt rc ON rc.RoleKey = h.RoleKey
        JOIN #RoleSvc rs ON rs.RoleKey = h.RoleKey AND rs.rn = (h.h3 % rc.Cnt) + 1
        WHERE h.wd <> 6                                              -- closed Sundays
          AND NOT (h.pSeq % 3 = 0 AND h.wd = h.pSeq % 6)             -- practitioner weekday off
          AND h.h1 < CASE WHEN h.d < -1 THEN 80 WHEN h.d = -1 THEN 140 WHEN h.d = 0 THEN 300
                          WHEN h.d = 1 THEN 250 ELSE 110 END
    ),
    Fin AS (
        SELECT pk.*, DATEADD(minute, pk.Dur, pk.StartAt) AS EndAt,
               1 + (pk.h5 % 70) + (pk.LocSeq - 1) * 50 AS CustN
        FROM Pick pk
    )
    SELECT NEWID() AS BkId, f.*,
           CASE
               WHEN f.EndAt < @Now THEN
                    CASE WHEN f.h4 < 58 THEN 5 WHEN f.h4 < 78 THEN 6 WHEN f.h4 < 88 THEN 1 WHEN f.h4 < 96 THEN 3 ELSE 2 END
               WHEN f.StartAt <= @Now THEN 4
               ELSE CASE WHEN f.h4 < 60 THEN 1 WHEN f.h4 < 85 THEN 0 WHEN f.h4 < 93 THEN 3 ELSE 2 END
           END AS Status
    INTO #Bk
    FROM Fin f
    WHERE NOT EXISTS (SELECT 1 FROM booking.staff_leave l
                      WHERE l.TenantId = @TenantId AND l.StaffMemberId = f.StaffId
                        AND f.StartAt < l.EndAt AND f.EndAt > l.StartAt)
      AND NOT EXISTS (SELECT 1 FROM booking.business_closed_dates cd
                      WHERE cd.TenantId = @TenantId AND cd.BusinessLocationId = f.LocId
                        AND f.DayLocal BETWEEN cd.StartDate AND cd.EndDate);

    INSERT booking.bookings
        (Id, TenantId, ServiceOfferingId, CustomerId, StaffMemberId, BusinessLocationId, GuestCount, StartAt, EndAt,
         Status, SettlementSource, PaidAt, ExternalPaymentReference, CustomerNotes, BusinessNotes, IdempotencyKey,
         CreatedAt, UpdatedAt)
    SELECT b.BkId, @TenantId, b.SvcId, cu.CustomerId, b.StaffId, b.LocId, 1, b.StartAt, b.EndAt,
           b.Status,
           IIF(b.Status = 6, 1 + (b.h6 % 2), 0),
           IIF(b.Status = 6, b.EndAt, NULL),
           CASE WHEN b.Status = 6 AND b.h6 % 2 = 1 THEN CONCAT(N'TILL-', RIGHT(CONCAT('00000', b.h7 % 100000), 5))
                WHEN b.Status = 6 THEN CONCAT(N'seed_ps_', b.h7 % 1000000) END,
           CASE WHEN b.h6 % 100 < 40 THEN cn.Txt END,
           CASE WHEN b.Status IN (5, 6) AND b.h7 % 100 < 20 THEN bn.Txt END,
           CONCAT(N'seed-lekki-demo-bk-', b.k),
           DATEADD(day, -(1 + b.h7 % 9), b.StartAt),
           IIF(b.EndAt < @Now, b.EndAt, DATEADD(day, -(1 + b.h7 % 9), b.StartAt))
    FROM #Bk b
    JOIN #Cust cu ON cu.n = b.CustN
    LEFT JOIN #Txt cn ON cn.Kind = 'cnote' AND cn.i = b.h6 % (SELECT Cnt FROM #TxtCnt WHERE Kind = 'cnote')
    LEFT JOIN #Txt bn ON bn.Kind = 'bnote' AND bn.i = b.h7 % (SELECT Cnt FROM #TxtCnt WHERE Kind = 'bnote');
    PRINT CONCAT('bookings inserted: ', @@ROWCOUNT);

    INSERT booking.booking_lines (Id, BookingId, TenantId, ServiceOfferingId, SortOrder, PriceAmount, DurationMinutes, ServiceName, IsAddOn)
    SELECT NEWID(), b.BkId, @TenantId, b.SvcId, 0, b.Price, b.Dur, b.SvcName, 0 FROM #Bk b;
    PRINT CONCAT('booking_lines inserted: ', @@ROWCOUNT);
END
ELSE PRINT 'bookings: already seeded - skipped';

------------------------------------------------------------------------------
-- 5. Reviews on past Completed / Paid seeded bookings (~65%)
------------------------------------------------------------------------------
INSERT booking.reviews
    (Id, TenantId, BookingId, CustomerId, Rating, Comment, OwnerReply, OwnerReplyAt, IsHidden, CreatedAt, HiddenAt)
SELECT NEWID(), @TenantId, x.Id, x.CustomerId, x.Rating, rt.Txt,
       CASE WHEN x.Rating <= 3 AND x.h % 10 < 7 THEN rp.Txt
            WHEN x.Rating >= 4 AND x.h % 10 < 4 THEN rp.Txt END,
       CASE WHEN (x.Rating <= 3 AND x.h % 10 < 7) OR (x.Rating >= 4 AND x.h % 10 < 4)
            THEN IIF(DATEADD(hour, 3 + x.h % 18, x.CreatedAt) > @Now, @Now, DATEADD(hour, 3 + x.h % 18, x.CreatedAt)) END,
       x.Hidden,
       x.CreatedAt,
       IIF(x.Hidden = 1, IIF(DATEADD(day, 1, x.CreatedAt) > @Now, @Now, DATEADD(day, 1, x.CreatedAt)), NULL)
FROM (
    SELECT b.Id, b.CustomerId,
           CAST(CASE WHEN r.v < 52 THEN 5 WHEN r.v < 80 THEN 4 WHEN r.v < 91 THEN 3 WHEN r.v < 97 THEN 2 ELSE 1 END AS TINYINT) AS Rating,
           r.h,
           IIF(r.h % 50 = 7, CAST(1 AS BIT), CAST(0 AS BIT)) AS Hidden,
           IIF(DATEADD(hour, 2 + r.h % 70, b.EndAt) > @Now, @Now, DATEADD(hour, 2 + r.h % 70, b.EndAt)) AS CreatedAt
    FROM booking.bookings b
    CROSS APPLY (SELECT ABS(CAST(CHECKSUM(CONCAT(b.IdempotencyKey, N'rv')) AS BIGINT)) AS h) r0
    CROSS APPLY (SELECT r0.h AS h, r0.h % 100 AS v) r
    WHERE b.TenantId = @TenantId
      AND b.IdempotencyKey LIKE N'seed-lekki-demo-bk-%'
      AND b.Status IN (5, 6)
      AND b.EndAt < DATEADD(day, -1, @Now)
      AND ABS(CAST(CHECKSUM(CONCAT(b.IdempotencyKey, N'rvp')) AS BIGINT)) % 100 < 65
      AND NOT EXISTS (SELECT 1 FROM booking.reviews rv WHERE rv.BookingId = b.Id)
) x
JOIN #TxtCnt rc ON rc.Kind = CONCAT('rv', x.Rating)
JOIN #Txt rt ON rt.Kind = CONCAT('rv', x.Rating) AND rt.i = (x.h / 100) % rc.Cnt
JOIN #TxtCnt pc ON pc.Kind = IIF(x.Rating >= 4, 'rpos', 'rneg')
LEFT JOIN #Txt rp ON rp.Kind = IIF(x.Rating >= 4, 'rpos', 'rneg') AND rp.i = (x.h / 1000) % pc.Cnt;
PRINT CONCAT('reviews inserted: ', @@ROWCOUNT);

------------------------------------------------------------------------------
-- 6. Messaging: booking-scoped threads per branch (branch filter uses booking context)
--    + tenant-wide profile threads. Unread = last customer message newer than owner read cursor.
------------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM messaging.conversation_messages
               WHERE TenantId = @TenantId AND ClientMessageId LIKE N'seed-lekki-demo-%')
BEGIN
    CREATE TABLE #Conv (
        ConvId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY, CustomerId UNIQUEIDENTIFIER NOT NULL,
        ContextType INT NOT NULL, ContextId UNIQUEIDENTIFIER NULL,
        T CHAR(1) NOT NULL,   -- A customer only | B owner replied then customer again (unread) | C caught up | F FAQ bot | X customer follow-up burst
        Closed BIT NOT NULL, Tc DATETIMEOFFSET(7) NOT NULL, H BIGINT NOT NULL,
        CustPart UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(), BizPart UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        BotPart UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        M1 UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(), M2 UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(), M3 UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID());

    -- Booking threads: 28 per branch from recent / near-future seeded bookings
    INSERT #Conv (ConvId, CustomerId, ContextType, ContextId, T, Closed, Tc, H)
    SELECT NEWID(), q.CustomerId, 1, q.Id,
           CASE WHEN q.t < 8 THEN IIF(q.t % 3 = 0, 'X', 'A') WHEN q.t < 13 THEN 'B' ELSE 'C' END,
           IIF(q.t = 19, 1, 0),
           DATEADD(minute, -(30 + q.h % 3000), @Now), q.h
    FROM (
        SELECT b.Id, b.CustomerId, r.h, ROW_NUMBER() OVER (PARTITION BY b.BusinessLocationId ORDER BY r.h) % 20 AS t,
               ROW_NUMBER() OVER (PARTITION BY b.BusinessLocationId ORDER BY r.h) AS rn
        FROM booking.bookings b
        CROSS APPLY (SELECT ABS(CAST(CHECKSUM(CONCAT(b.IdempotencyKey, N'cv')) AS BIGINT)) AS h) r
        WHERE b.TenantId = @TenantId AND b.IdempotencyKey LIKE N'seed-lekki-demo-bk-%'
          AND b.Status IN (0, 1, 3, 4, 5, 6)
          AND b.StartAt BETWEEN DATEADD(day, -14, @Now) AND DATEADD(day, 7, @Now)
    ) q
    WHERE q.rn <= 28;

    -- Profile threads (tenant-wide): 4 FAQ-bot, 4 answered, 4 waiting
    INSERT #Conv (ConvId, CustomerId, ContextType, ContextId, T, Closed, Tc, H)
    SELECT NEWID(), cu.CustomerId, 3, NULL,
           CASE WHEN n.n <= 4 THEN 'F' WHEN n.n <= 8 THEN 'C' ELSE 'A' END, 0,
           DATEADD(minute, -(90 + n.n * 173), @Now),
           ABS(CAST(CHECKSUM(CONCAT(N'pf', n.n)) AS BIGINT))
    FROM #N n JOIN #Cust cu ON cu.n = n.n
    WHERE n.n <= 12;

    INSERT messaging.conversations (Id, TenantId, CustomerId, ContextType, ContextId, [Status], CreatedAt, LastMessageAt, CreatedByParticipantId)
    SELECT ConvId, @TenantId, CustomerId, ContextType, ContextId, IIF(Closed = 1, 1, 0), Tc,
           CASE T WHEN 'A' THEN Tc WHEN 'X' THEN DATEADD(minute, 7, Tc) WHEN 'B' THEN DATEADD(minute, 35, Tc)
                  WHEN 'C' THEN DATEADD(minute, 10, Tc) WHEN 'F' THEN DATEADD(minute, 1, Tc) END,
           CustPart
    FROM #Conv;
    PRINT CONCAT('conversations inserted: ', @@ROWCOUNT);

    INSERT messaging.conversation_participants
        (Id, ConversationId, TenantId, ParticipantType, CustomerId, BusinessUserId, BotKey, Auth0Sub, LastReadMessageId, LastReadAt, IsMuted, JoinedAt)
    SELECT CustPart, ConvId, @TenantId, 0, CustomerId, NULL, NULL, NULL,
           CASE T WHEN 'X' THEN M2 WHEN 'B' THEN M3 WHEN 'C' THEN M2 WHEN 'F' THEN M2 ELSE M1 END,
           CASE T WHEN 'X' THEN DATEADD(minute, 7, Tc) WHEN 'B' THEN DATEADD(minute, 35, Tc) WHEN 'C' THEN DATEADD(minute, 10, Tc)
                  WHEN 'F' THEN DATEADD(minute, 1, Tc) ELSE Tc END, 0, Tc
    FROM #Conv
    UNION ALL
    SELECT BizPart, ConvId, @TenantId, 1, NULL, @OwnerBusinessUserId, NULL, @OwnerAuth0Sub, M2, DATEADD(minute, IIF(T = 'B', 13, 11), Tc), 0, DATEADD(minute, 10, Tc)
    FROM #Conv WHERE T IN ('B', 'C')
    UNION ALL
    SELECT BotPart, ConvId, @TenantId, 2, NULL, NULL, N'faq', NULL, NULL, NULL, 0, DATEADD(minute, 1, Tc)
    FROM #Conv WHERE T = 'F';

    -- m1: customer opening message
    INSERT messaging.conversation_messages (Id, ConversationId, TenantId, SenderParticipantId, [Type], Body, ClientMessageId, CreatedAt, ModerationStatus)
    SELECT c.M1, c.ConvId, @TenantId, c.CustPart, 0, tx.Txt,
           CONCAT(N'seed-lekki-demo-m1-', REPLACE(CONVERT(VARCHAR(36), c.ConvId), '-', '')), c.Tc, 0
    FROM #Conv c
    JOIN #TxtCnt k ON k.Kind = IIF(c.ContextType = 3, 'pmsg', 'cmsg')
    JOIN #Txt tx ON tx.Kind = k.Kind AND tx.i = c.H % k.Cnt;

    -- m2: customer follow-up (X), owner reply (B, C) or FAQ bot (F)
    INSERT messaging.conversation_messages (Id, ConversationId, TenantId, SenderParticipantId, [Type], Body, ClientMessageId, CreatedAt, ModerationStatus)
    SELECT c.M2, c.ConvId, @TenantId,
           CASE c.T WHEN 'X' THEN c.CustPart WHEN 'F' THEN c.BotPart ELSE c.BizPart END,
           0,
           CASE WHEN c.T = 'F' THEN N'Thanks for asking! Check our live availability and book online on Adeni - we confirm slots in real time.'
                ELSE tx.Txt END,
           CONCAT(N'seed-lekki-demo-m2-', REPLACE(CONVERT(VARCHAR(36), c.ConvId), '-', '')),
           CASE c.T WHEN 'X' THEN DATEADD(minute, 7, c.Tc) WHEN 'F' THEN DATEADD(minute, 1, c.Tc) ELSE DATEADD(minute, IIF(c.T = 'B', 12, 10), c.Tc) END,
           0
    FROM #Conv c
    LEFT JOIN #TxtCnt k ON k.Kind = IIF(c.T = 'X', 'cfup', 'omsg')
    LEFT JOIN #Txt tx ON tx.Kind = k.Kind AND tx.i = (c.H / 7) % k.Cnt
    WHERE c.T IN ('X', 'B', 'C', 'F');

    -- m3: customer replies again after owner (B) -> counts as unread for the team
    INSERT messaging.conversation_messages (Id, ConversationId, TenantId, SenderParticipantId, [Type], Body, ClientMessageId, CreatedAt, ModerationStatus)
    SELECT c.M3, c.ConvId, @TenantId, c.CustPart, 0, tx.Txt,
           CONCAT(N'seed-lekki-demo-m3-', REPLACE(CONVERT(VARCHAR(36), c.ConvId), '-', '')),
           DATEADD(minute, 35, c.Tc), 0
    FROM #Conv c
    JOIN #TxtCnt k ON k.Kind = 'cfup'
    JOIN #Txt tx ON tx.Kind = 'cfup' AND tx.i = (c.H / 13) % k.Cnt
    WHERE c.T = 'B';
    DECLARE @MsgCount INT = (SELECT COUNT(*) FROM messaging.conversation_messages WHERE TenantId = @TenantId AND ClientMessageId LIKE N'seed-lekki-demo-%');
    PRINT CONCAT('conversation_messages inserted: ', @MsgCount);
END
ELSE PRINT 'messaging: already seeded - skipped';

------------------------------------------------------------------------------
-- 7. Queue tickets for today (13 per branch, every status)
------------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM booking.queue_tickets WHERE TenantId = @TenantId AND Notes LIKE N'seed-lekki-demo-v1%')
BEGIN
    INSERT booking.queue_tickets
        (Id, TenantId, BusinessLocationId, ServiceOfferingId, StaffMemberId, CustomerId, TenantCustomerId, GuestName, GuestPhone,
         GuestEmail, [Status], PartySize, Notes, PublicToken, BookingId, [Source], JoinedAt, CalledAt, StartedAt, CompletedAt,
         NoShowAt, CreatedAt, UpdatedAt)
    SELECT NEWID(), @TenantId, L.LocId, rs.SvcId,
           CASE WHEN st.Status IN (1, 2, 3, 4) THEN pr.StaffId END,
           IIF(st.i % 2 = 0, cu.CustomerId, NULL),
           tc.Id,
           tc.DisplayName, CONCAT(N'+', tc.NormalizedPhone), tc.Email,
           st.Status, IIF(st.i % 3 = 0, 2, 1),
           CONCAT(N'seed-lekki-demo-v1 queue #', st.i),
           LOWER(REPLACE(CONVERT(VARCHAR(36), NEWID()), '-', '')),
           NULL, 0,
           DATEADD(minute, -((14 - st.i) * 8), @Now),
           CASE WHEN st.Status IN (1, 2, 3, 4) THEN DATEADD(minute, -((14 - st.i) * 8) + 12, @Now) END,
           CASE WHEN st.Status IN (2, 3) THEN DATEADD(minute, -((14 - st.i) * 8) + 18, @Now) END,
           CASE WHEN st.Status = 3 THEN DATEADD(minute, -((14 - st.i) * 8) + 45, @Now) END,
           CASE WHEN st.Status = 4 THEN DATEADD(minute, -((14 - st.i) * 8) + 25, @Now) END,
           DATEADD(minute, -((14 - st.i) * 8), @Now), @Now
    FROM #Loc L
    CROSS JOIN (VALUES (1, 0), (2, 0), (3, 0), (4, 0), (5, 0), (6, 1), (7, 2), (8, 2), (9, 3), (10, 3), (11, 3), (12, 4), (13, 5)) st(i, Status)
    JOIN #Prac pr ON pr.LocSeq = L.Seq AND pr.Seq = st.i
    JOIN #RoleSvcCnt rc ON rc.RoleKey = pr.RoleKey
    JOIN #RoleSvc rs ON rs.RoleKey = pr.RoleKey AND rs.rn = (st.i % rc.Cnt) + 1
    LEFT JOIN #Cust cu ON cu.n = 20 + st.i + (L.Seq - 1) * 50
    LEFT JOIN tenancy.tenant_customers tc ON tc.TenantId = @TenantId
         AND tc.Id = IIF(st.i % 2 = 0, cu.TenantCustomerId,
                         (SELECT t.Id FROM tenancy.tenant_customers t
                          WHERE t.TenantId = @TenantId
                            AND t.NormalizedPhone = CONCAT(N'2348032', RIGHT(CONCAT('000000', st.i + (L.Seq - 1) * 6), 6))));
    PRINT CONCAT('queue_tickets inserted: ', @@ROWCOUNT);
END
ELSE PRINT 'queue_tickets: already seeded - skipped';

------------------------------------------------------------------------------
-- 8. Refresh CRM recency from seeded bookings
------------------------------------------------------------------------------
UPDATE tc
SET LastBookingAt = x.LastStart,
    LastSeenAt = IIF(x.LastStart > tc.LastSeenAt, x.LastStart, tc.LastSeenAt),
    UpdatedAt = @Now
FROM tenancy.tenant_customers tc
JOIN (SELECT b.CustomerId, MAX(b.StartAt) AS LastStart
      FROM booking.bookings b
      WHERE b.TenantId = @TenantId AND b.IdempotencyKey LIKE N'seed-lekki-demo-bk-%'
        AND b.Status IN (1, 4, 5, 6) AND b.StartAt <= @Now
      GROUP BY b.CustomerId) x ON x.CustomerId = tc.CustomerId
WHERE tc.TenantId = @TenantId;

COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

------------------------------------------------------------------------------
-- Report
------------------------------------------------------------------------------
SELECT bl.Slug AS Branch,
       (SELECT COUNT(*) FROM booking.staff_members s WHERE s.TenantId = @TenantId AND s.BusinessLocationId = bl.LocId AND s.IsActive = 1) AS ActiveStaff,
       (SELECT COUNT(*) FROM booking.bookings b WHERE b.TenantId = @TenantId AND b.BusinessLocationId = bl.LocId) AS Bookings,
       (SELECT COUNT(*) FROM booking.bookings b WHERE b.TenantId = @TenantId AND b.BusinessLocationId = bl.LocId AND CAST(SWITCHOFFSET(b.StartAt, '+01:00') AS DATE) = @TodayLocal) AS BookingsToday,
       (SELECT COUNT(*) FROM booking.bookings b WHERE b.TenantId = @TenantId AND b.BusinessLocationId = bl.LocId AND b.StartAt > @Now) AS BookingsUpcoming,
       (SELECT COUNT(*) FROM booking.reviews r JOIN booking.bookings b ON b.Id = r.BookingId WHERE b.BusinessLocationId = bl.LocId) AS Reviews,
       (SELECT COUNT(*) FROM messaging.conversations c JOIN booking.bookings b ON b.Id = c.ContextId AND c.ContextType = 1 WHERE c.TenantId = @TenantId AND b.BusinessLocationId = bl.LocId) AS BookingThreads,
       (SELECT COUNT(*) FROM booking.queue_tickets q WHERE q.TenantId = @TenantId AND q.BusinessLocationId = bl.LocId) AS QueueTickets,
       (SELECT COUNT(*) FROM booking.staff_leave l JOIN booking.staff_members s ON s.Id = l.StaffMemberId WHERE s.BusinessLocationId = bl.LocId) AS StaffLeave,
       (SELECT COUNT(*) FROM booking.business_closed_dates d WHERE d.BusinessLocationId = bl.LocId) AS ClosedDates
FROM #Loc bl ORDER BY bl.Seq;

SELECT (SELECT COUNT(*) FROM tenancy.tenant_customers WHERE TenantId = @TenantId) AS TenantCustomers,
       (SELECT COUNT(*) FROM messaging.conversations WHERE TenantId = @TenantId) AS Conversations,
       (SELECT COUNT(*) FROM messaging.conversation_messages WHERE TenantId = @TenantId) AS Messages,
       (SELECT COUNT(*) FROM booking.reviews WHERE TenantId = @TenantId) AS Reviews,
       (SELECT COUNT(*) FROM booking.bookings WHERE TenantId = @TenantId) AS Bookings;
