using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameJobPostApplicationDeadlineToEndDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = 'public'
                            AND table_name = 'JobPosts'
                            AND column_name = 'ApplicationDeadline'
                    ) AND EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = 'public'
                            AND table_name = 'JobPosts'
                            AND column_name = 'EndDate'
                    ) THEN
                        RAISE EXCEPTION 'Both JobPosts.ApplicationDeadline and JobPosts.EndDate exist. Resolve manually before applying migration.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = 'public'
                            AND table_name = 'JobPosts'
                            AND column_name = 'ApplicationDeadline'
                    ) AND NOT EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = 'public'
                            AND table_name = 'JobPosts'
                            AND column_name = 'EndDate'
                    ) THEN
                        ALTER TABLE "JobPosts" RENAME COLUMN "ApplicationDeadline" TO "EndDate";
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM pg_indexes
                        WHERE schemaname = 'public'
                            AND tablename = 'JobPosts'
                            AND indexname = 'IX_JobPosts_ApplicationDeadline'
                    ) AND NOT EXISTS (
                        SELECT 1
                        FROM pg_indexes
                        WHERE schemaname = 'public'
                            AND tablename = 'JobPosts'
                            AND indexname = 'IX_JobPosts_EndDate'
                    ) THEN
                        ALTER INDEX "IX_JobPosts_ApplicationDeadline" RENAME TO "IX_JobPosts_EndDate";
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = 'public'
                            AND table_name = 'JobPosts'
                            AND column_name = 'EndDate'
                    ) AND EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = 'public'
                            AND table_name = 'JobPosts'
                            AND column_name = 'ApplicationDeadline'
                    ) THEN
                        RAISE EXCEPTION 'Both JobPosts.EndDate and JobPosts.ApplicationDeadline exist. Resolve manually before reverting migration.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = 'public'
                            AND table_name = 'JobPosts'
                            AND column_name = 'EndDate'
                    ) AND NOT EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = 'public'
                            AND table_name = 'JobPosts'
                            AND column_name = 'ApplicationDeadline'
                    ) THEN
                        ALTER TABLE "JobPosts" RENAME COLUMN "EndDate" TO "ApplicationDeadline";
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM pg_indexes
                        WHERE schemaname = 'public'
                            AND tablename = 'JobPosts'
                            AND indexname = 'IX_JobPosts_EndDate'
                    ) AND NOT EXISTS (
                        SELECT 1
                        FROM pg_indexes
                        WHERE schemaname = 'public'
                            AND tablename = 'JobPosts'
                            AND indexname = 'IX_JobPosts_ApplicationDeadline'
                    ) THEN
                        ALTER INDEX "IX_JobPosts_EndDate" RENAME TO "IX_JobPosts_ApplicationDeadline";
                    END IF;
                END $$;
                """);
        }
    }
}
