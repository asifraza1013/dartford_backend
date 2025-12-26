using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace inflan_api.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add new payment columns to Campaigns table
            migrationBuilder.AddColumn<int>(
                name: "PaymentType",
                table: "Campaigns",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<bool>(
                name: "IsRecurringEnabled",
                table: "Campaigns",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "TotalAmountInPence",
                table: "Campaigns",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "PaidAmountInPence",
                table: "Campaigns",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "ReleasedToInfluencerInPence",
                table: "Campaigns",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            // Add new columns to Transactions table for multi-gateway support
            migrationBuilder.AddColumn<string>(
                name: "TransactionReference",
                table: "Transactions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MilestoneId",
                table: "Transactions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "AmountInPence",
                table: "Transactions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "PlatformFeeInPence",
                table: "Transactions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "TotalAmountInPence",
                table: "Transactions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "Gateway",
                table: "Transactions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GatewayPaymentId",
                table: "Transactions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GatewayTransactionId",
                table: "Transactions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PaymentMethodId",
                table: "Transactions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RedirectUrl",
                table: "Transactions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WebhookPayload",
                table: "Transactions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Transactions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailureCode",
                table: "Transactions",
                type: "text",
                nullable: true);

            // Create unique index on TransactionReference
            migrationBuilder.CreateIndex(
                name: "IX_Transactions_TransactionReference",
                table: "Transactions",
                column: "TransactionReference",
                unique: true);

            // Create PaymentMilestones table
            migrationBuilder.CreateTable(
                name: "PaymentMilestones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CampaignId = table.Column<int>(type: "integer", nullable: false),
                    MilestoneNumber = table.Column<int>(type: "integer", nullable: false),
                    AmountInPence = table.Column<long>(type: "bigint", nullable: false),
                    PlatformFeeInPence = table.Column<long>(type: "bigint", nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TransactionId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentMilestones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentMilestones_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Create PaymentMethods table
            migrationBuilder.CreateTable(
                name: "PaymentMethods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Gateway = table.Column<string>(type: "text", nullable: false),
                    AuthorizationCode = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: true),
                    CardType = table.Column<string>(type: "text", nullable: true),
                    Last4 = table.Column<string>(type: "text", nullable: true),
                    ExpiryMonth = table.Column<string>(type: "text", nullable: true),
                    ExpiryYear = table.Column<string>(type: "text", nullable: true),
                    Bank = table.Column<string>(type: "text", nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentMethods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentMethods_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Create Invoices table
            migrationBuilder.CreateTable(
                name: "Invoices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InvoiceNumber = table.Column<string>(type: "text", nullable: false),
                    CampaignId = table.Column<int>(type: "integer", nullable: false),
                    BrandId = table.Column<int>(type: "integer", nullable: false),
                    InfluencerId = table.Column<int>(type: "integer", nullable: false),
                    MilestoneId = table.Column<int>(type: "integer", nullable: true),
                    TransactionId = table.Column<int>(type: "integer", nullable: true),
                    SubtotalInPence = table.Column<long>(type: "bigint", nullable: false),
                    PlatformFeeInPence = table.Column<long>(type: "bigint", nullable: false),
                    TotalAmountInPence = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PdfPath = table.Column<string>(type: "text", nullable: true),
                    IssuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Invoices_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Create InfluencerPayouts table
            migrationBuilder.CreateTable(
                name: "InfluencerPayouts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CampaignId = table.Column<int>(type: "integer", nullable: false),
                    InfluencerId = table.Column<int>(type: "integer", nullable: false),
                    MilestoneId = table.Column<int>(type: "integer", nullable: true),
                    GrossAmountInPence = table.Column<long>(type: "bigint", nullable: false),
                    PlatformFeeInPence = table.Column<long>(type: "bigint", nullable: false),
                    NetAmountInPence = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ReleasedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PayoutReference = table.Column<string>(type: "text", nullable: true),
                    FailureMessage = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InfluencerPayouts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InfluencerPayouts_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Create PlatformSettings table
            migrationBuilder.CreateTable(
                name: "PlatformSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SettingKey = table.Column<string>(type: "text", nullable: false),
                    SettingValue = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformSettings", x => x.Id);
                });

            // Create indexes
            migrationBuilder.CreateIndex(
                name: "IX_PaymentMilestones_CampaignId",
                table: "PaymentMilestones",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentMethods_UserId",
                table: "PaymentMethods",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentMethods_AuthorizationCode",
                table: "PaymentMethods",
                column: "AuthorizationCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_CampaignId",
                table: "Invoices",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_InvoiceNumber",
                table: "Invoices",
                column: "InvoiceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InfluencerPayouts_CampaignId",
                table: "InfluencerPayouts",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_InfluencerPayouts_InfluencerId",
                table: "InfluencerPayouts",
                column: "InfluencerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformSettings_SettingKey",
                table: "PlatformSettings",
                column: "SettingKey",
                unique: true);

            // Insert default platform settings using raw SQL
            migrationBuilder.Sql(@"
                INSERT INTO ""PlatformSettings"" (""SettingKey"", ""SettingValue"", ""Description"", ""CreatedAt"", ""UpdatedAt"")
                VALUES ('BrandPlatformFeePercent', '2.0', 'Platform fee percentage charged to brands', NOW(), NOW()),
                       ('InfluencerPlatformFeePercent', '2.0', 'Platform fee percentage deducted from influencer payouts', NOW(), NOW())
                ON CONFLICT (""SettingKey"") DO NOTHING;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop new tables
            migrationBuilder.DropTable(name: "PlatformSettings");
            migrationBuilder.DropTable(name: "InfluencerPayouts");
            migrationBuilder.DropTable(name: "Invoices");
            migrationBuilder.DropTable(name: "PaymentMethods");
            migrationBuilder.DropTable(name: "PaymentMilestones");

            // Remove columns from Transactions
            migrationBuilder.DropIndex(name: "IX_Transactions_TransactionReference", table: "Transactions");
            migrationBuilder.DropColumn(name: "TransactionReference", table: "Transactions");
            migrationBuilder.DropColumn(name: "MilestoneId", table: "Transactions");
            migrationBuilder.DropColumn(name: "AmountInPence", table: "Transactions");
            migrationBuilder.DropColumn(name: "PlatformFeeInPence", table: "Transactions");
            migrationBuilder.DropColumn(name: "TotalAmountInPence", table: "Transactions");
            migrationBuilder.DropColumn(name: "Gateway", table: "Transactions");
            migrationBuilder.DropColumn(name: "GatewayPaymentId", table: "Transactions");
            migrationBuilder.DropColumn(name: "GatewayTransactionId", table: "Transactions");
            migrationBuilder.DropColumn(name: "PaymentMethodId", table: "Transactions");
            migrationBuilder.DropColumn(name: "RedirectUrl", table: "Transactions");
            migrationBuilder.DropColumn(name: "WebhookPayload", table: "Transactions");
            migrationBuilder.DropColumn(name: "UpdatedAt", table: "Transactions");
            migrationBuilder.DropColumn(name: "FailureCode", table: "Transactions");

            // Remove columns from Campaigns
            migrationBuilder.DropColumn(name: "PaymentType", table: "Campaigns");
            migrationBuilder.DropColumn(name: "IsRecurringEnabled", table: "Campaigns");
            migrationBuilder.DropColumn(name: "TotalAmountInPence", table: "Campaigns");
            migrationBuilder.DropColumn(name: "PaidAmountInPence", table: "Campaigns");
            migrationBuilder.DropColumn(name: "ReleasedToInfluencerInPence", table: "Campaigns");
        }
    }
}
