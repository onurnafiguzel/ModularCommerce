using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ModularCommerce.Payment.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentRefund : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RefundTransactionId",
                schema: "payment",
                table: "payments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RefundedAtUtc",
                schema: "payment",
                table: "payments",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RefundTransactionId",
                schema: "payment",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "RefundedAtUtc",
                schema: "payment",
                table: "payments");
        }
    }
}
