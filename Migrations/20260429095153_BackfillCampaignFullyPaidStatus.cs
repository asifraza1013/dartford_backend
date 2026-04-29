using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace inflan_api.Migrations
{
    /// <summary>
    /// One-shot data fix for campaigns whose PaymentStatus stayed at PARTIAL (6)
    /// even after every milestone was paid. Two passes:
    ///
    ///   1. Backfill TotalAmountInPence from the legacy Amount field on rows
    ///      where it's still 0/NULL but Amount is populated. This rescues older
    ///      campaigns that pre-date the InPence aggregate columns and would
    ///      otherwise never satisfy the amount-based "fully paid" check.
    ///
    ///   2. Flip PaymentStatus from PARTIAL → COMPLETED on campaigns where the
    ///      milestone ledger is fully PAID (Status = 2). PaymentCompletedAt is
    ///      set to NOW() if it was never recorded.
    ///
    /// Both UPDATEs are idempotent and safe to re-run.
    /// </summary>
    public partial class BackfillCampaignFullyPaidStatus : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Backfill TotalAmountInPence from legacy Amount where missing.
            migrationBuilder.Sql(@"
                UPDATE ""Campaigns""
                SET ""TotalAmountInPence"" = ROUND(""Amount""::numeric * 100)::bigint
                WHERE (""TotalAmountInPence"" IS NULL OR ""TotalAmountInPence"" <= 0)
                  AND ""Amount"" IS NOT NULL
                  AND ""Amount"" > 0;
            ");

            // 2. Flip stuck-PARTIAL campaigns to COMPLETED when every milestone is PAID.
            //    PaymentStatus: 3=COMPLETED, 6=PARTIAL.  MilestoneStatus: 2=PAID.
            migrationBuilder.Sql(@"
                UPDATE ""Campaigns"" AS c
                SET ""PaymentStatus"" = 3,
                    ""PaymentCompletedAt"" = COALESCE(c.""PaymentCompletedAt"", NOW())
                WHERE c.""PaymentStatus"" = 6
                  AND EXISTS (
                      SELECT 1 FROM ""PaymentMilestones"" m
                      WHERE m.""CampaignId"" = c.""Id""
                  )
                  AND NOT EXISTS (
                      SELECT 1 FROM ""PaymentMilestones"" m
                      WHERE m.""CampaignId"" = c.""Id""
                        AND m.""Status"" <> 2
                  );
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Pure data fix; no schema change to roll back. Reverting the value
            // updates would require an audit log we don't keep, so Down() is a
            // no-op rather than an unsafe pseudo-revert.
        }
    }
}
