using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkshopHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWaitlistAndIntegrityConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Registrations_SessionId",
                table: "Registrations");

            migrationBuilder.DropIndex(
                name: "IX_Registrations_UserId",
                table: "Registrations");

            migrationBuilder.AddColumn<int>(
                name: "WaitlistCount",
                table: "Sessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "PromotedAtUtc",
                table: "Registrations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Sessions_Capacity_Positive",
                table: "Sessions",
                sql: "\"Capacity\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Sessions_SeatsTaken_Range",
                table: "Sessions",
                sql: "\"SeatsTaken\" >= 0 AND \"SeatsTaken\" <= \"Capacity\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Sessions_WaitlistCount_NonNegative",
                table: "Sessions",
                sql: "\"WaitlistCount\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_Registrations_Session_Status_CreatedAt",
                table: "Registrations",
                columns: new[] { "SessionId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Registrations_UserId_CreatedAtUtc",
                table: "Registrations",
                columns: new[] { "UserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_Registrations_Session_User_Active",
                table: "Registrations",
                columns: new[] { "SessionId", "UserId" },
                unique: true,
                filter: "\"Status\" <> 'Cancelled'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Sessions_Capacity_Positive",
                table: "Sessions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Sessions_SeatsTaken_Range",
                table: "Sessions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Sessions_WaitlistCount_NonNegative",
                table: "Sessions");

            migrationBuilder.DropIndex(
                name: "IX_Registrations_Session_Status_CreatedAt",
                table: "Registrations");

            migrationBuilder.DropIndex(
                name: "IX_Registrations_UserId_CreatedAtUtc",
                table: "Registrations");

            migrationBuilder.DropIndex(
                name: "UX_Registrations_Session_User_Active",
                table: "Registrations");

            migrationBuilder.DropColumn(
                name: "WaitlistCount",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "PromotedAtUtc",
                table: "Registrations");

            migrationBuilder.CreateIndex(
                name: "IX_Registrations_SessionId",
                table: "Registrations",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Registrations_UserId",
                table: "Registrations",
                column: "UserId");
        }
    }
}
