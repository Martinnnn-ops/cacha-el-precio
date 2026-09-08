using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Product_Service.Migrations
{
    /// <inheritdoc />
    public partial class IntegrateScraperContract : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Active",
                table: "Products",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Products",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "Products",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductImage",
                table: "Products",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductUrl",
                table: "Products",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Store",
                table: "Products",
                type: "TEXT",
                maxLength: 60,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_Store_ExternalId",
                table: "Products",
                columns: new[] { "Store", "ExternalId" },
                unique: true,
                filter: "\"Store\" IS NOT NULL AND \"ExternalId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_Store_ExternalId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Active",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ProductImage",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ProductUrl",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Store",
                table: "Products");
        }
    }
}
