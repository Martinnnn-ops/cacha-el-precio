using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Product_Service.Migrations
{
    /// <inheritdoc />
    public partial class AddProductSlug : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Slug",
                schema: "product",
                table: "Products",
                type: "character varying(180)",
                maxLength: 180,
                nullable: true);

            migrationBuilder.Sql(
                """
                WITH raw_candidates AS (
                    SELECT
                        "ProductId",
                        "CanonicalKey",
                        COALESCE(
                            NULLIF(
                                LEFT(
                                    TRIM(BOTH '-' FROM REGEXP_REPLACE(
                                        LOWER("CanonicalKey"),
                                        '[^a-z0-9]+',
                                        '-',
                                        'g'
                                    )),
                                    180
                                ),
                                ''
                            ),
                            'producto-' || "ProductId"
                        ) AS candidate
                    FROM product."Products"
                ),
                candidates AS (
                    SELECT
                        "ProductId",
                        "CanonicalKey",
                        CASE
                            WHEN candidate ~ '^[0-9]+$' THEN 'producto-' || candidate
                            ELSE candidate
                        END AS candidate
                    FROM raw_candidates
                ),
                ranked AS (
                    SELECT
                        "ProductId",
                        "CanonicalKey",
                        candidate,
                        ROW_NUMBER() OVER (
                            PARTITION BY candidate
                            ORDER BY "ProductId"
                        ) AS occurrence
                    FROM candidates
                )
                UPDATE product."Products" AS product
                SET "Slug" = CASE
                    WHEN ranked.occurrence = 1 THEN ranked.candidate
                    ELSE LEFT(ranked.candidate, 167)
                        || '-'
                        || LEFT(MD5(ranked."CanonicalKey"), 12)
                END
                FROM ranked
                WHERE product."ProductId" = ranked."ProductId";
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Slug",
                schema: "product",
                table: "Products",
                type: "character varying(180)",
                maxLength: 180,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(180)",
                oldMaxLength: 180,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_Slug",
                schema: "product",
                table: "Products",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_Slug",
                schema: "product",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Slug",
                schema: "product",
                table: "Products");
        }
    }
}
