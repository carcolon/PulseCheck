using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PulseCheck.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTransformationalLeaderCandidateCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TransformationalLeaderCandidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SolvoId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    CorporateEmail = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    JobTitleCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Operation = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    Client = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    Department = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastSyncedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransformationalLeaderCandidates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TransformationalLeaderCandidates_CorporateEmail",
                table: "TransformationalLeaderCandidates",
                column: "CorporateEmail");

            migrationBuilder.CreateIndex(
                name: "IX_TransformationalLeaderCandidates_IsActive",
                table: "TransformationalLeaderCandidates",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_TransformationalLeaderCandidates_Operation",
                table: "TransformationalLeaderCandidates",
                column: "Operation");

            migrationBuilder.CreateIndex(
                name: "IX_TransformationalLeaderCandidates_SolvoId",
                table: "TransformationalLeaderCandidates",
                column: "SolvoId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TransformationalLeaderCandidates");
        }
    }
}
