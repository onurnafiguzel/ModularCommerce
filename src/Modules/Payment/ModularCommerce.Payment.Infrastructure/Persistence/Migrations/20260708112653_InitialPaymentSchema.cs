using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ModularCommerce.Payment.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialPaymentSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "payment");

            migrationBuilder.CreateTable(
                name: "payments",
                schema: "payment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PspTransactionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClaimedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "payment_attempts",
                schema: "payment",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    Outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PspTransactionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LatencyMs = table.Column<long>(type: "bigint", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_attempts", x => x.id);
                    table.ForeignKey(
                        name: "FK_payment_attempts_payments_payment_id",
                        column: x => x.payment_id,
                        principalSchema: "payment",
                        principalTable: "payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_payment_attempts_payment_id",
                schema: "payment",
                table: "payment_attempts",
                column: "payment_id");

            migrationBuilder.CreateIndex(
                name: "IX_payments_OrderId",
                schema: "payment",
                table: "payments",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "ix_payments_customer_id_idempotency_key",
                schema: "payment",
                table: "payments",
                columns: new[] { "CustomerId", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payment_attempts",
                schema: "payment");

            migrationBuilder.DropTable(
                name: "payments",
                schema: "payment");
        }
    }
}
