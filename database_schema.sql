-- PostgreSQL Database Schema Export
-- Database: Inflan API
-- Generated: 2025-11-12
-- Description: Complete schema with all tables, columns, indexes, and foreign keys

-- ============================================
-- Table: Users
-- Description: Main user table for both brands and influencers
-- ============================================
CREATE TABLE "Users" (
    "Id" SERIAL PRIMARY KEY,
    "Name" TEXT,
    "UserName" TEXT,
    "Email" TEXT,
    "Password" TEXT,
    "UserType" INTEGER NOT NULL,
    "Status" INTEGER NOT NULL,
    "ProfileImage" TEXT,
    "BrandName" TEXT,
    "BrandCategory" TEXT,
    "BrandSector" TEXT,
    "Goals" TEXT[]
);

-- ============================================
-- Table: Influencers
-- Description: Influencer profile information with social media accounts
-- ============================================
CREATE TABLE "Influencers" (
    "Id" SERIAL PRIMARY KEY,
    "UserId" INTEGER NOT NULL,
    "YouTube" TEXT,
    "Instagram" TEXT,
    "Facebook" TEXT,
    "TikTok" TEXT,
    "YouTubeFollower" INTEGER NOT NULL,
    "InstagramFollower" INTEGER NOT NULL,
    "FacebookFollower" INTEGER NOT NULL,
    "TikTokFollower" INTEGER NOT NULL,
    "Bio" TEXT,
    CONSTRAINT "FK_Influencers_Users_UserId" FOREIGN KEY ("UserId")
        REFERENCES "Users" ("Id") ON DELETE CASCADE
);

-- ============================================
-- Table: Plans
-- Description: Subscription and service plans
-- ============================================
CREATE TABLE "Plans" (
    "Id" SERIAL PRIMARY KEY,
    "UserId" INTEGER NOT NULL,
    "PlanName" TEXT,
    "Price" REAL NOT NULL,
    "Currency" TEXT,
    "Interval" TEXT,
    "NumberOfMonths" INTEGER NOT NULL,
    "Status" INTEGER NOT NULL,
    "PlanDetails" TEXT[],
    CONSTRAINT "FK_Plans_Users_UserId" FOREIGN KEY ("UserId")
        REFERENCES "Users" ("Id") ON DELETE CASCADE
);

-- ============================================
-- Table: Campaigns
-- Description: Marketing campaigns between brands and influencers
-- ============================================
CREATE TABLE "Campaigns" (
    "Id" SERIAL PRIMARY KEY,
    "BrandId" INTEGER NOT NULL,
    "InfluencerId" INTEGER NOT NULL,
    "PlanId" INTEGER NOT NULL,
    "CampaignName" TEXT,
    "CampaignStartDate" DATE NOT NULL,
    "CampaignEndDate" DATE NOT NULL,
    "CampaignStatus" INTEGER NOT NULL,
    "PaymentStatus" INTEGER NOT NULL,
    "Amount" REAL NOT NULL,
    "Currency" TEXT,
    "InstructionDocuments" TEXT[],
    CONSTRAINT "FK_Campaigns_Users_BrandId" FOREIGN KEY ("BrandId")
        REFERENCES "Users" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_Campaigns_Users_InfluencerId" FOREIGN KEY ("InfluencerId")
        REFERENCES "Users" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_Campaigns_Plans_PlanId" FOREIGN KEY ("PlanId")
        REFERENCES "Plans" ("Id") ON DELETE CASCADE
);

-- ============================================
-- Table: Transactions
-- Description: Payment transactions for campaigns
-- ============================================
CREATE TABLE "Transactions" (
    "Id" SERIAL PRIMARY KEY,
    "UserId" INTEGER NOT NULL,
    "CampaignId" INTEGER NOT NULL,
    "TransactionId" TEXT,
    "StripePaymentIntentId" TEXT,
    "Amount" REAL NOT NULL,
    "Currency" TEXT,
    "TransactionStatus" INTEGER NOT NULL,
    "CreatedAt" TIMESTAMP WITH TIME ZONE NOT NULL,
    "CompletedAt" TIMESTAMP WITH TIME ZONE,
    "FailureMessage" TEXT,
    CONSTRAINT "FK_Transactions_Users_UserId" FOREIGN KEY ("UserId")
        REFERENCES "Users" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_Transactions_Campaigns_CampaignId" FOREIGN KEY ("CampaignId")
        REFERENCES "Campaigns" ("Id") ON DELETE CASCADE
);

-- ============================================
-- Indexes
-- ============================================

-- Influencers indexes
CREATE INDEX "IX_Influencers_UserId" ON "Influencers" ("UserId");

-- Plans indexes
CREATE INDEX "IX_Plans_UserId" ON "Plans" ("UserId");

-- Campaigns indexes
CREATE INDEX "IX_Campaigns_BrandId" ON "Campaigns" ("BrandId");
CREATE INDEX "IX_Campaigns_InfluencerId" ON "Campaigns" ("InfluencerId");
CREATE INDEX "IX_Campaigns_PlanId" ON "Campaigns" ("PlanId");

-- Transactions indexes
CREATE INDEX "IX_Transactions_UserId" ON "Transactions" ("UserId");
CREATE INDEX "IX_Transactions_CampaignId" ON "Transactions" ("CampaignId");
CREATE UNIQUE INDEX "IX_Transactions_TransactionId" ON "Transactions" ("TransactionId");

-- ============================================
-- Comments on Tables and Columns
-- ============================================

-- Users Table
COMMENT ON TABLE "Users" IS 'Main user table storing both brand and influencer user accounts';
COMMENT ON COLUMN "Users"."UserType" IS 'User type: 0 = Brand, 1 = Influencer';
COMMENT ON COLUMN "Users"."Status" IS 'Account status: 0 = Inactive, 1 = Active';
COMMENT ON COLUMN "Users"."Goals" IS 'Array of user goals/objectives';

-- Influencers Table
COMMENT ON TABLE "Influencers" IS 'Extended profile information for influencer users';
COMMENT ON COLUMN "Influencers"."UserId" IS 'Foreign key reference to Users table';

-- Plans Table
COMMENT ON TABLE "Plans" IS 'Subscription and service plans offered by users';
COMMENT ON COLUMN "Plans"."Status" IS 'Plan status: 0 = Inactive, 1 = Active';
COMMENT ON COLUMN "Plans"."PlanDetails" IS 'Array of plan feature details';

-- Campaigns Table
COMMENT ON TABLE "Campaigns" IS 'Marketing campaigns connecting brands with influencers';
COMMENT ON COLUMN "Campaigns"."BrandId" IS 'Foreign key to Users table (brand user)';
COMMENT ON COLUMN "Campaigns"."InfluencerId" IS 'Foreign key to Users table (influencer user)';
COMMENT ON COLUMN "Campaigns"."CampaignStatus" IS 'Campaign status enum';
COMMENT ON COLUMN "Campaigns"."PaymentStatus" IS 'Payment status enum';
COMMENT ON COLUMN "Campaigns"."InstructionDocuments" IS 'Array of instruction document URLs';

-- Transactions Table
COMMENT ON TABLE "Transactions" IS 'Payment transactions for campaign payments';
COMMENT ON COLUMN "Transactions"."TransactionId" IS 'Unique transaction identifier';
COMMENT ON COLUMN "Transactions"."StripePaymentIntentId" IS 'Stripe payment intent ID';
COMMENT ON COLUMN "Transactions"."TransactionStatus" IS 'Transaction status enum';
