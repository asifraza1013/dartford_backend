using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace inflan_api.Migrations
{
    /// <inheritdoc />
    public partial class AddInfluencerBankAccountsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InfluencerBankAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InfluencerId = table.Column<int>(type: "integer", nullable: false),
                    BankName = table.Column<string>(type: "text", nullable: false),
                    BankCode = table.Column<string>(type: "text", nullable: false),
                    AccountNumberLast4 = table.Column<string>(type: "text", nullable: false),
                    AccountName = table.Column<string>(type: "text", nullable: false),
                    PaystackRecipientCode = table.Column<string>(type: "text", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InfluencerBankAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InfluencerBankAccounts_Users_InfluencerId",
                        column: x => x.InfluencerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InfluencerBankAccounts_InfluencerId",
                table: "InfluencerBankAccounts",
                column: "InfluencerId");

            migrationBuilder.CreateIndex(
                name: "IX_InfluencerBankAccounts_PaystackRecipientCode",
                table: "InfluencerBankAccounts",
                column: "PaystackRecipientCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InfluencerBankAccounts");
        }
    }
}
