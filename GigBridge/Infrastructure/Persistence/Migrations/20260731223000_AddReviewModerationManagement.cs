using Domain.Enums.Elo;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations;

[DbContext(typeof(GigbridgeDbContext))]
[Migration("20260731223000_AddReviewModerationManagement")]
public partial class AddReviewModerationManagement : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<int>(
            name: "Reason",
            table: "UserEloPointTransactions",
            type: "integer",
            nullable: false,
            comment: "Enum UserEloPointReason: 0=InitialGrant, 1=InactivityPenalty, 2=ReturnBonus, 3=JobCompletion, 4=ReviewRating, 5=LegacyIntegrityPenalty, 6=ReviewModeration",
            oldClrType: typeof(int),
            oldType: "integer",
            oldComment: "Enum UserEloPointReason: 0=InitialGrant, 1=InactivityPenalty, 2=ReturnBonus, 3=JobCompletion, 4=ReviewRating, 5=LegacyIntegrityPenalty");

        migrationBuilder.AddColumn<DateTime>(
            name: "ModeratedAt",
            table: "Reviews",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "ModeratedByAdminId",
            table: "Reviews",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ModerationNote",
            table: "Reviews",
            type: "character varying(1000)",
            maxLength: 1000,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "ModerationStatus",
            table: "Reviews",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.CreateIndex(
            name: "IX_Reviews_ModeratedByAdminId",
            table: "Reviews",
            column: "ModeratedByAdminId");

        migrationBuilder.CreateIndex(
            name: "IX_Reviews_ModerationStatus",
            table: "Reviews",
            column: "ModerationStatus");

        migrationBuilder.AddForeignKey(
            name: "Reviews_usr_ModeratedByAdminId_fkey",
            table: "Reviews",
            column: "ModeratedByAdminId",
            principalTable: "Users",
            principalColumn: "UserId",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.Sql("""
            WITH moderation_candidates AS (
                SELECT DISTINCT ON (review."ReviewsId")
                    review."ReviewsId",
                    report."ResolvedAt",
                    report."ResolvedByAdminId",
                    report."AdminNote"
                FROM "Reviews" review
                INNER JOIN "Reports" report
                    ON report."ReportedEntityType" = 'Review'
                    AND report."ReportedEntityId" = review."ReviewsId"
                    AND report."Status" = 2
                    AND report."ResolvedAt" IS NOT NULL
                WHERE review."IsVisible" = FALSE
                    AND review."UpdatedAt" IS NOT NULL
                    AND ABS(EXTRACT(EPOCH FROM (review."UpdatedAt" - report."ResolvedAt"))) <= 60
                ORDER BY review."ReviewsId", report."ResolvedAt" DESC
            )
            UPDATE "Reviews" review
            SET "ModerationStatus" = 1,
                "ModeratedAt" = candidate."ResolvedAt",
                "ModeratedByAdminId" = candidate."ResolvedByAdminId",
                "ModerationNote" = COALESCE(candidate."AdminNote", 'Backfilled from resolved review report')
            FROM moderation_candidates candidate
            WHERE review."ReviewsId" = candidate."ReviewsId";
            """);

        migrationBuilder.Sql("""
            DO $$
            DECLARE
                item RECORD;
                points_before INTEGER;
                points_after INTEGER;
                requested_delta INTEGER;
            BEGIN
                FOR item IN
                    SELECT review."ReviewsId", review."RevieweeId", review."ModeratedAt",
                           COALESCE(SUM(transaction."PointsDelta") FILTER (
                               WHERE transaction."Reason" IN (3, 4)
                           ), 0)::INTEGER AS original_delta
                    FROM "Reviews" review
                    LEFT JOIN "UserEloPointTransactions" transaction
                        ON transaction."SourceEntityType" = 'Review'
                        AND transaction."SourceEntityId" = review."ReviewsId"
                    WHERE review."ModerationStatus" = 1
                    GROUP BY review."ReviewsId", review."RevieweeId", review."ModeratedAt"
                    ORDER BY review."ModeratedAt", review."ReviewsId"
                LOOP
                    IF NOT EXISTS (
                        SELECT 1 FROM "UserEloPointTransactions"
                        WHERE "IdempotencyKey" = 'review-moderation:' || item."ReviewsId" || ':hide:legacy'
                    ) THEN
                        SELECT "CurrentPoints" INTO points_before
                        FROM "UserEloScores"
                        WHERE "UserId" = item."RevieweeId"
                        FOR UPDATE;

                        IF points_before IS NOT NULL THEN
                            requested_delta := -item.original_delta;
                            points_after := GREATEST(0, points_before + requested_delta);

                            INSERT INTO "UserEloPointTransactions" (
                                "UserEloPointTransactionsId", "UserId", "PointsDelta", "PointsBefore",
                                "PointsAfter", "Reason", "SourceEntityType", "SourceEntityId",
                                "IdempotencyKey", "Metadata", "CreatedAt")
                            VALUES (
                                gen_random_uuid(), item."RevieweeId", points_after - points_before, points_before,
                                points_after, 6, 'Review', item."ReviewsId",
                                'review-moderation:' || item."ReviewsId" || ':hide:legacy',
                                jsonb_build_object('action', 'hide', 'source', 'legacy_report_backfill',
                                                   'requestedDelta', requested_delta)::text,
                                COALESCE(item."ModeratedAt", now()));

                            UPDATE "UserEloScores"
                            SET "CurrentPoints" = points_after, "UpdatedAt" = now()
                            WHERE "UserId" = item."RevieweeId";
                        END IF;
                    END IF;
                END LOOP;
            END $$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            WITH moderation_delta AS (
                SELECT "UserId", SUM("PointsDelta")::INTEGER AS delta
                FROM "UserEloPointTransactions"
                WHERE "Reason" = 6 AND "SourceEntityType" = 'Review'
                GROUP BY "UserId"
            )
            UPDATE "UserEloScores" score
            SET "CurrentPoints" = GREATEST(0, score."CurrentPoints" - moderation_delta.delta),
                "UpdatedAt" = now()
            FROM moderation_delta
            WHERE score."UserId" = moderation_delta."UserId";

            DELETE FROM "UserEloPointTransactions"
            WHERE "Reason" = 6 AND "SourceEntityType" = 'Review';
            """);

        migrationBuilder.DropForeignKey(name: "Reviews_usr_ModeratedByAdminId_fkey", table: "Reviews");
        migrationBuilder.DropIndex(name: "IX_Reviews_ModeratedByAdminId", table: "Reviews");
        migrationBuilder.DropIndex(name: "IX_Reviews_ModerationStatus", table: "Reviews");
        migrationBuilder.DropColumn(name: "ModeratedAt", table: "Reviews");
        migrationBuilder.DropColumn(name: "ModeratedByAdminId", table: "Reviews");
        migrationBuilder.DropColumn(name: "ModerationNote", table: "Reviews");
        migrationBuilder.DropColumn(name: "ModerationStatus", table: "Reviews");

        migrationBuilder.AlterColumn<int>(
            name: "Reason",
            table: "UserEloPointTransactions",
            type: "integer",
            nullable: false,
            comment: "Enum UserEloPointReason: 0=InitialGrant, 1=InactivityPenalty, 2=ReturnBonus, 3=JobCompletion, 4=ReviewRating, 5=LegacyIntegrityPenalty",
            oldClrType: typeof(int),
            oldType: "integer",
            oldComment: "Enum UserEloPointReason: 0=InitialGrant, 1=InactivityPenalty, 2=ReturnBonus, 3=JobCompletion, 4=ReviewRating, 5=LegacyIntegrityPenalty, 6=ReviewModeration");
    }
}
