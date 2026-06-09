-- Demo data for the admin Reports & Analytics page.
-- Idempotent-ish: clears the demo id ranges first so it can be re-run.
BEGIN;

DELETE FROM "ChatMessages"  WHERE "ConversationId" BETWEEN 501 AND 599;
DELETE FROM "Conversations" WHERE "Id" BETWEEN 501 AND 599;
DELETE FROM "Ratings"       WHERE "Id" BETWEEN 401 AND 499;
DELETE FROM "Transactions"  WHERE "Id" BETWEEN 301 AND 399;
DELETE FROM "Campaigns"     WHERE "Id" BETWEEN 201 AND 299;
DELETE FROM "Users"         WHERE "Id" BETWEEN 101 AND 199;

-- ---- Users: 3 brands + 4 influencers, registered across Jan–Apr 2026 ----
INSERT INTO "Users" ("Id","Name","BrandName","Email","Password","UserType","Status","IsEmailVerified","VerificationAttempts","Location","Currency","CreatedAt") VALUES
(101,'Acme Marketing','Acme Marketing','acme@example.com','x',2,1,true,0,'GB','GBP','2026-01-15'),
(102,'Zen Foods','Zen Foods','zen@example.com','x',2,1,true,0,'NG','NGN','2026-02-10'),
(103,'Nova Tech','Nova Tech','nova@example.com','x',2,1,true,0,'GB','GBP','2026-03-05'),
(110,'Aisha Kola',NULL,'aisha@example.com','x',3,1,true,0,'NG','NGN','2026-01-20'),
(111,'Tom Bailey',NULL,'tom@example.com','x',3,1,true,0,'GB','GBP','2026-02-15'),
(112,'Lola Mensah',NULL,'lola@example.com','x',3,1,true,0,'NG','NGN','2026-03-12'),
(113,'Sam Pierce',NULL,'sam@example.com','x',3,1,true,0,'GB','GBP','2026-04-08');

-- ---- Campaigns (PlanId 1 exists). Status: 2=Rejected 5=AwaitingPayment 6=Active 7=Completed 8=Cancelled ----
-- PaymentStatus: 3=Completed for paid ones.
INSERT INTO "Campaigns"
("Id","PlanId","ProjectName","CampaignStartDate","CampaignEndDate","BrandId","InfluencerId","CampaignStatus","PaymentStatus","Amount","Currency","PaymentType","IsRecurringEnabled","TotalAmountInPence","PaidAmountInPence","ReleasedToInfluencerInPence","CreatedAt","ContractSignedAt","SignatureApprovedAt","InfluencerAcceptedAt","PaymentCompletedAt") VALUES
-- Acme (101) — 5 campaigns
(201,1,'Acme Spring Launch','2026-01-10','2026-02-09',101,111,7,3,1000,'GBP',1,false,100000,100000,90000,'2026-01-08','2026-01-09','2026-01-09','2026-01-09','2026-01-12'),
(202,1,'Acme Summer Promo','2026-02-01','2026-03-17',101,113,7,3,1500,'GBP',1,false,150000,150000,135000,'2026-01-30','2026-01-31','2026-01-31','2026-01-31','2026-02-05'),
(203,1,'Acme Brand Refresh','2026-04-01','2026-05-31',101,111,6,3,2000,'GBP',1,false,200000,200000,180000,'2026-03-28','2026-03-29','2026-03-29','2026-03-29','2026-04-02'),
(204,1,'Acme Pop-up','2026-03-01','2026-03-31',101,113,8,1,800,'GBP',1,false,80000,0,0,'2026-02-25','2026-02-26','2026-02-26','2026-02-26',NULL),
(205,1,'Acme Autumn Teaser','2026-05-10','2026-06-10',101,111,5,1,1200,'GBP',1,false,120000,0,0,'2026-05-05','2026-05-06','2026-05-06','2026-05-06',NULL),
-- Zen Foods (102) — 4 campaigns
(206,1,'Zen Recipe Series','2026-02-05','2026-03-07',102,110,7,3,300000,'NGN',1,false,30000000,30000000,27000000,'2026-02-03','2026-02-04','2026-02-04','2026-02-04','2026-02-08'),
(207,1,'Zen Healthy Living','2026-03-01','2026-04-15',102,112,7,3,450000,'NGN',1,false,45000000,45000000,40500000,'2026-02-27','2026-02-28','2026-02-28','2026-02-28','2026-03-05'),
(208,1,'Zen Snack Drop','2026-04-20','2026-06-20',102,110,6,3,500000,'NGN',1,false,50000000,50000000,45000000,'2026-04-18','2026-04-19','2026-04-19','2026-04-19','2026-04-22'),
(209,1,'Zen Festival','2026-03-15','2026-04-15',102,112,2,1,250000,'NGN',1,false,25000000,0,0,'2026-03-10',NULL,NULL,NULL,NULL),
-- Nova Tech (103) — 3 campaigns
(210,1,'Nova App Reveal','2026-03-10','2026-04-09',103,113,7,3,900,'GBP',1,false,90000,90000,81000,'2026-03-08','2026-03-09','2026-03-09','2026-03-09','2026-04-09'),
(211,1,'Nova Gadget Review','2026-04-01','2026-05-16',103,111,7,3,1100,'GBP',1,false,110000,110000,99000,'2026-03-29','2026-03-30','2026-03-30','2026-03-30','2026-05-16'),
(212,1,'Nova Smart Home','2026-05-01','2026-06-30',103,113,6,3,1300,'GBP',1,false,130000,130000,117000,'2026-04-28','2026-04-29','2026-04-29','2026-04-29','2026-05-02'),
-- Existing brand (1) — 2 campaigns
(213,1,'Legacy NG Campaign','2026-01-20','2026-02-19',1,110,7,3,200000,'NGN',1,false,20000000,20000000,18000000,'2026-01-18','2026-01-19','2026-01-19','2026-01-19','2026-01-22'),
(214,1,'Legacy Cancelled','2026-02-01','2026-03-01',1,2,8,1,150000,'NGN',1,false,15000000,0,0,'2026-01-28','2026-01-29','2026-01-29','2026-01-29',NULL);

-- ---- Transactions (COMPLETED=3) for paid campaigns; fee ~10%; CompletedAt spread across months ----
INSERT INTO "Transactions"
("Id","UserId","CampaignId","Amount","AmountInPence","PlatformFeeInPence","TotalAmountInPence","Currency","Gateway","TransactionStatus","TransactionReference","CreatedAt","CompletedAt") VALUES
(301,101,201,1000,100000,10000,110000,'GBP','stripe',3,'TXN-201','2026-01-12','2026-01-12'),
(302,101,202,1500,150000,15000,165000,'GBP','stripe',3,'TXN-202','2026-02-05','2026-02-05'),
(303,101,203,2000,200000,20000,220000,'GBP','stripe',3,'TXN-203','2026-04-02','2026-04-02'),
(304,102,206,300000,30000000,3000000,33000000,'NGN','paystack',3,'TXN-206','2026-02-08','2026-02-08'),
(305,102,207,450000,45000000,4500000,49500000,'NGN','paystack',3,'TXN-207','2026-03-05','2026-03-05'),
(306,102,208,500000,50000000,5000000,55000000,'NGN','paystack',3,'TXN-208','2026-04-22','2026-04-22'),
(307,103,210,900,90000,9000,99000,'GBP','stripe',3,'TXN-210','2026-04-09','2026-04-09'),
(308,103,211,1100,110000,11000,121000,'GBP','stripe',3,'TXN-211','2026-05-16','2026-05-16'),
(309,103,212,1300,130000,13000,143000,'GBP','stripe',3,'TXN-212','2026-05-02','2026-05-02'),
(310,1,213,200000,20000000,2000000,22000000,'NGN','paystack',3,'TXN-213','2026-01-22','2026-01-22');

-- ---- Ratings for COMPLETED campaigns (both directions). RateeUserType: 2=Brand 3=Influencer ----
INSERT INTO "Ratings" ("Id","CampaignId","RaterId","RateeId","RateeUserType","Stars","Comment","CreatedAt") VALUES
(401,201,101,111,3,5,'Great work',           '2026-02-10'),
(402,201,111,101,2,4,'Smooth payment',        '2026-02-10'),
(403,202,101,113,3,4,'Solid content',         '2026-03-18'),
(404,202,113,101,2,5,'Easy to work with',     '2026-03-18'),
(405,206,102,110,3,5,'Amazing recipes',       '2026-03-08'),
(406,206,110,102,2,5,'Lovely brand',          '2026-03-08'),
(407,207,102,112,3,3,'Decent',                '2026-04-16'),
(408,207,112,102,2,4,'Good comms',            '2026-04-16'),
(409,210,103,113,3,5,'Top tier',              '2026-04-10'),
(410,210,113,103,2,4,'Prompt',                '2026-04-10'),
(411,211,103,111,3,4,'Reliable',              '2026-05-17'),
(412,211,111,103,2,5,'Great brief',           '2026-05-17'),
(413,213,1,110,3,4,'Nice',                    '2026-02-20'),
(414,213,110,1,2,3,'Ok',                       '2026-02-20');

-- ---- Conversations + Chat messages (varied volume per sender) ----
INSERT INTO "Conversations" ("Id","BrandId","InfluencerId","CreatedAt","LastMessageAt","IsDeletedByBrand","IsDeletedByInfluencer") VALUES
(501,101,111,'2026-01-08','2026-04-20',false,false),
(502,102,110,'2026-02-03','2026-04-22',false,false),
(503,103,113,'2026-03-08','2026-05-02',false,false),
(504,101,113,'2026-01-30','2026-03-18',false,false);

-- messages: (conv, sender, recipient, count)
INSERT INTO "ChatMessages" ("ConversationId","SenderId","RecipientId","Content","MessageType","CreatedAt","IsRead","IsDeletedBySender","IsDeletedByRecipient")
SELECT 501,111,101,'msg '||g,1,'2026-04-01'::timestamptz + (g||' hours')::interval,true,false,false FROM generate_series(1,15) g;
INSERT INTO "ChatMessages" ("ConversationId","SenderId","RecipientId","Content","MessageType","CreatedAt","IsRead","IsDeletedBySender","IsDeletedByRecipient")
SELECT 501,101,111,'msg '||g,1,'2026-04-02'::timestamptz + (g||' hours')::interval,true,false,false FROM generate_series(1,8) g;
INSERT INTO "ChatMessages" ("ConversationId","SenderId","RecipientId","Content","MessageType","CreatedAt","IsRead","IsDeletedBySender","IsDeletedByRecipient")
SELECT 502,110,102,'msg '||g,1,'2026-04-10'::timestamptz + (g||' hours')::interval,true,false,false FROM generate_series(1,12) g;
INSERT INTO "ChatMessages" ("ConversationId","SenderId","RecipientId","Content","MessageType","CreatedAt","IsRead","IsDeletedBySender","IsDeletedByRecipient")
SELECT 502,102,110,'msg '||g,1,'2026-04-11'::timestamptz + (g||' hours')::interval,true,false,false FROM generate_series(1,6) g;
INSERT INTO "ChatMessages" ("ConversationId","SenderId","RecipientId","Content","MessageType","CreatedAt","IsRead","IsDeletedBySender","IsDeletedByRecipient")
SELECT 503,113,103,'msg '||g,1,'2026-05-01'::timestamptz + (g||' hours')::interval,true,false,false FROM generate_series(1,9) g;
INSERT INTO "ChatMessages" ("ConversationId","SenderId","RecipientId","Content","MessageType","CreatedAt","IsRead","IsDeletedBySender","IsDeletedByRecipient")
SELECT 503,103,113,'msg '||g,1,'2026-05-02'::timestamptz + (g||' hours')::interval,true,false,false FROM generate_series(1,5) g;
INSERT INTO "ChatMessages" ("ConversationId","SenderId","RecipientId","Content","MessageType","CreatedAt","IsRead","IsDeletedBySender","IsDeletedByRecipient")
SELECT 504,113,101,'msg '||g,1,'2026-03-10'::timestamptz + (g||' hours')::interval,true,false,false FROM generate_series(1,4) g;
INSERT INTO "ChatMessages" ("ConversationId","SenderId","RecipientId","Content","MessageType","CreatedAt","IsRead","IsDeletedBySender","IsDeletedByRecipient")
SELECT 504,101,113,'msg '||g,1,'2026-03-11'::timestamptz + (g||' hours')::interval,true,false,false FROM generate_series(1,7) g;

-- ---- Reset identity sequences so app inserts don't collide with explicit ids ----
SELECT setval(pg_get_serial_sequence('"Users"','Id'),         (SELECT MAX("Id") FROM "Users"));
SELECT setval(pg_get_serial_sequence('"Campaigns"','Id'),     (SELECT MAX("Id") FROM "Campaigns"));
SELECT setval(pg_get_serial_sequence('"Transactions"','Id'),  (SELECT MAX("Id") FROM "Transactions"));
SELECT setval(pg_get_serial_sequence('"Ratings"','Id'),       (SELECT MAX("Id") FROM "Ratings"));
SELECT setval(pg_get_serial_sequence('"Conversations"','Id'), (SELECT MAX("Id") FROM "Conversations"));
SELECT setval(pg_get_serial_sequence('"ChatMessages"','Id'),  (SELECT MAX("Id") FROM "ChatMessages"));

COMMIT;
