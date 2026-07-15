using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ModularCommerce.Discovery.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialDiscoverySchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // pgvector uzantısı — vektör kolonu için şart. Superuser gerektirir (dev'de postgres;
            // prod'da least-privilege modül kullanıcısı ise önceden bir superuser kurmalı).
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS vector;");

            migrationBuilder.EnsureSchema(
                name: "discovery");

            // Skaler kolonlar EF tarafından üretildi; vektör kolonu EF'e görünmez olduğundan (Ignore)
            // aşağıda raw SQL ile eklenir.
            migrationBuilder.CreateTable(
                name: "product_embeddings",
                schema: "discovery",
                columns: table => new
                {
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceTextHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_embeddings", x => x.ProductId);
                });

            // Embedding kolonu (pgvector). Boyut = EmbeddingConstants.Dimensions (1536) — kod sabitiyle eş.
            migrationBuilder.Sql(
                "ALTER TABLE discovery.product_embeddings ADD COLUMN \"Embedding\" vector(1536) NOT NULL;");

            // Kosinüs benzerliği için HNSW yaklaşık-en-yakın-komşu index'i (`<=>` = vector_cosine_ops).
            migrationBuilder.Sql(
                "CREATE INDEX ix_product_embeddings_embedding ON discovery.product_embeddings " +
                "USING hnsw (\"Embedding\" vector_cosine_ops);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_embeddings",
                schema: "discovery");
        }
    }
}
