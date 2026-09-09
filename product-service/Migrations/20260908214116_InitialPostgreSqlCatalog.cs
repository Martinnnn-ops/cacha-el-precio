using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Product_Service.Migrations
{
    /// <inheritdoc />
    public partial class InitialPostgreSqlCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "product");

            migrationBuilder.CreateTable(
                name: "Products",
                schema: "product",
                columns: table => new
                {
                    ProductId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CanonicalKey = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    ProductName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ProductBrand = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ProductCategory = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ProductImage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.ProductId);
                });

            migrationBuilder.CreateTable(
                name: "ProductOffers",
                schema: "product",
                columns: table => new
                {
                    OfferId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Store = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Price = table.Column<int>(type: "integer", nullable: false),
                    Sizes = table.Column<string[]>(type: "text[]", nullable: false),
                    ProductUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ProductImage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductOffers", x => x.OfferId);
                    table.ForeignKey(
                        name: "FK_ProductOffers_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "product",
                        principalTable: "Products",
                        principalColumn: "ProductId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductOffers_ProductId",
                schema: "product",
                table: "ProductOffers",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductOffers_Store_ExternalId",
                schema: "product",
                table: "ProductOffers",
                columns: new[] { "Store", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_CanonicalKey",
                schema: "product",
                table: "Products",
                column: "CanonicalKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductOffers",
                schema: "product");

            migrationBuilder.DropTable(
                name: "Products",
                schema: "product");
        }
    }
}
