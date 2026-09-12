using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PulseCheck.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCampaignSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAtUtc",
                table: "Campaigns",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Campaigns",
                keyColumn: "Id",
                keyValue: new Guid("4ec08ed3-257a-40bf-ac31-78cab5bd4fa4"),
                column: "DeletedAtUtc",
                value: null);

            migrationBuilder.UpdateData(
                table: "Campaigns",
                keyColumn: "Id",
                keyValue: new Guid("5cd9666b-b621-46d9-a2b7-2ae8db4decc8"),
                column: "DeletedAtUtc",
                value: null);

            migrationBuilder.UpdateData(
                table: "Campaigns",
                keyColumn: "Id",
                keyValue: new Guid("7f0f8595-44ae-4f1e-ae98-a098ac2bd601"),
                column: "DeletedAtUtc",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "Campaigns");
        }
    }
}
