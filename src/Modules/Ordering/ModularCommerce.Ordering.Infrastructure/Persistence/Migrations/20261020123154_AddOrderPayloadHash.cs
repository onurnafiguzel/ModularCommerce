using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ModularCommerce.Ordering.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderPayloadHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PayloadHash",
                schema: "ordering",
                table: "orders",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PayloadHash",
                schema: "ordering",
                table: "orders");
        }
    }
}
