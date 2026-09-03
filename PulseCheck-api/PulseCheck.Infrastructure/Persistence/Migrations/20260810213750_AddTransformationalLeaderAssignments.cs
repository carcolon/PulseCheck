using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PulseCheck.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTransformationalLeaderAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TransformationalLeaderAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SolvoId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Operation = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransformationalLeaderAssignments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TransformationalLeaderAssignments_Operation",
                table: "TransformationalLeaderAssignments",
                column: "Operation");

            migrationBuilder.CreateIndex(
                name: "IX_TransformationalLeaderAssignments_SolvoId",
                table: "TransformationalLeaderAssignments",
                column: "SolvoId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TransformationalLeaderAssignments");
        }
    }
}
