using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PulseCheck.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTransformationalLeaderOperationScopes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OperationsJson",
                table: "TransformationalLeaderSessions",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "OperationsJson",
                table: "TransformationalLeaderExportJobs",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "OperationsJson",
                table: "TransformationalLeaderAssignments",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "[]");

            if (migrationBuilder.ActiveProvider.Contains("SqlServer"))
            {
                migrationBuilder.Sql("""
                    UPDATE [TransformationalLeaderAssignments]
                    SET [OperationsJson] = CASE
                        WHEN LTRIM(RTRIM([Operation])) = N'' THEN N'[]'
                        ELSE CONCAT(N'["', STRING_ESCAPE([Operation], 'json'), N'"]')
                    END
                    WHERE [OperationsJson] IS NULL OR [OperationsJson] = N'[]';

                    UPDATE [TransformationalLeaderSessions]
                    SET [OperationsJson] = CASE
                        WHEN LTRIM(RTRIM([Operation])) = N'' THEN N'[]'
                        ELSE CONCAT(N'["', STRING_ESCAPE([Operation], 'json'), N'"]')
                    END
                    WHERE [OperationsJson] IS NULL OR [OperationsJson] = N'[]';

                    UPDATE [TransformationalLeaderExportJobs]
                    SET [OperationsJson] = CASE
                        WHEN LTRIM(RTRIM([Operation])) = N'' THEN N'[]'
                        ELSE CONCAT(N'["', STRING_ESCAPE([Operation], 'json'), N'"]')
                    END
                    WHERE [OperationsJson] IS NULL OR [OperationsJson] = N'[]';
                    """);
            }
            else
            {
                migrationBuilder.Sql("""
                    UPDATE "TransformationalLeaderAssignments"
                    SET "OperationsJson" = CASE
                        WHEN trim("Operation") = '' THEN '[]'
                        ELSE '["' || replace("Operation", '"', '\"') || '"]'
                    END
                    WHERE "OperationsJson" IS NULL OR "OperationsJson" = '[]';

                    UPDATE "TransformationalLeaderSessions"
                    SET "OperationsJson" = CASE
                        WHEN trim("Operation") = '' THEN '[]'
                        ELSE '["' || replace("Operation", '"', '\"') || '"]'
                    END
                    WHERE "OperationsJson" IS NULL OR "OperationsJson" = '[]';

                    UPDATE "TransformationalLeaderExportJobs"
                    SET "OperationsJson" = CASE
                        WHEN trim("Operation") = '' THEN '[]'
                        ELSE '["' || replace("Operation", '"', '\"') || '"]'
                    END
                    WHERE "OperationsJson" IS NULL OR "OperationsJson" = '[]';
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OperationsJson",
                table: "TransformationalLeaderSessions");

            migrationBuilder.DropColumn(
                name: "OperationsJson",
                table: "TransformationalLeaderExportJobs");

            migrationBuilder.DropColumn(
                name: "OperationsJson",
                table: "TransformationalLeaderAssignments");
        }
    }
}
