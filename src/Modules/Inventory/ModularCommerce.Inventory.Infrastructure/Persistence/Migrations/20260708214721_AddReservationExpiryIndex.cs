using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ModularCommerce.Inventory.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationExpiryIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_reservations_active_expiry",
                schema: "inventory",
                table: "reservations",
                column: "ExpiresAtUtc",
                filter: "\"Status\" = 'Active'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_reservations_active_expiry",
                schema: "inventory",
                table: "reservations");
        }
    }
}
