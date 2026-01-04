using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace inflan_api.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountNumberFullToInfluencerBankAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // These columns may already exist if added manually via SQL
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'PaymentMethods' AND column_name = 'IsReusable') THEN
                        ALTER TABLE ""PaymentMethods"" ADD COLUMN ""IsReusable"" boolean NOT NULL DEFAULT false;
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'InfluencerBankAccounts' AND column_name = 'AccountNumberFull') THEN
                        ALTER TABLE ""InfluencerBankAccounts"" ADD COLUMN ""AccountNumberFull"" text;
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsReusable",
                table: "PaymentMethods");

            migrationBuilder.DropColumn(
                name: "AccountNumberFull",
                table: "InfluencerBankAccounts");
        }
    }
}
