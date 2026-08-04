using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RepairPremiumEntitlements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                table: "SubscriptionPlans",
                type: "text",
                nullable: true,
                defaultValueSql: "'VND'::character varying",
                oldClrType: typeof(string),
                oldType: "character varying(5)",
                oldMaxLength: 5,
                oldNullable: true,
                oldDefaultValueSql: "'VND'::character varying");

            migrationBuilder.Sql("""
                UPDATE "Subscriptions" AS subscription
                SET "Status" = 2,
                    "EndDate" = LEAST(subscription."EndDate", NOW()),
                    "AutoRenew" = FALSE,
                    "CancelledAt" = COALESCE(subscription."CancelledAt", NOW()),
                    "UpdatedAt" = NOW()
                FROM "SubscriptionPlans" AS plan,
                     "Users" AS app_user
                WHERE subscription."SubscriptionPlansId" = plan."SubscriptionPlansId"
                  AND subscription."UserId" = app_user."UserId"
                  AND subscription."Status" = 0
                  AND plan."TargetRole" IS NOT NULL
                  AND plan."TargetRole" <> app_user."Role";

                WITH users_with_entitlement_gaps AS (
                    SELECT future_subscription."UserId",
                           MIN(future_subscription."StartDate") AS first_future_start
                    FROM "Subscriptions" AS future_subscription
                    INNER JOIN "SubscriptionPlans" AS future_plan
                        ON future_plan."SubscriptionPlansId" =
                           future_subscription."SubscriptionPlansId"
                    INNER JOIN "Users" AS future_user
                        ON future_user."UserId" = future_subscription."UserId"
                    WHERE future_subscription."Status" = 0
                      AND future_subscription."StartDate" > NOW()
                      AND future_subscription."EndDate" >
                          future_subscription."StartDate"
                      AND future_plan."IsActive" = TRUE
                      AND future_plan."Price" > 0
                      AND (future_plan."TargetRole" IS NULL OR
                           future_plan."TargetRole" = future_user."Role")
                      AND NOT EXISTS (
                          SELECT 1
                          FROM "Subscriptions" AS current_subscription
                          INNER JOIN "SubscriptionPlans" AS current_plan
                              ON current_plan."SubscriptionPlansId" =
                                 current_subscription."SubscriptionPlansId"
                          WHERE current_subscription."UserId" =
                                    future_subscription."UserId"
                            AND current_subscription."Status" = 0
                            AND current_subscription."StartDate" <= NOW()
                            AND current_subscription."EndDate" > NOW()
                            AND current_plan."IsActive" = TRUE
                            AND current_plan."Price" > 0
                            AND (current_plan."TargetRole" IS NULL OR
                                 current_plan."TargetRole" = future_user."Role")
                      )
                    GROUP BY future_subscription."UserId"
                )
                UPDATE "Subscriptions" AS queued_subscription
                SET "StartDate" = queued_subscription."StartDate" -
                                  (gap.first_future_start - NOW()),
                    "EndDate" = queued_subscription."EndDate" -
                                (gap.first_future_start - NOW()),
                    "UpdatedAt" = NOW()
                FROM users_with_entitlement_gaps AS gap,
                     "SubscriptionPlans" AS queued_plan,
                     "Users" AS queued_user
                WHERE queued_subscription."UserId" = gap."UserId"
                  AND queued_subscription."SubscriptionPlansId" =
                      queued_plan."SubscriptionPlansId"
                  AND queued_subscription."UserId" = queued_user."UserId"
                  AND queued_subscription."Status" = 0
                  AND queued_subscription."StartDate" >= gap.first_future_start
                  AND queued_plan."IsActive" = TRUE
                  AND queued_plan."Price" > 0
                  AND (queued_plan."TargetRole" IS NULL OR
                       queued_plan."TargetRole" = queued_user."Role");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException(
                "RepairPremiumEntitlements preserves paid subscription time and cannot be safely reversed.");
        }
    }
}
