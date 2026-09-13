using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Product_Service.Migrations
{
    /// <inheritdoc />
    public partial class AddProductTaxonomy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BodyArea",
                schema: "product",
                table: "Products",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Cuerpo");

            migrationBuilder.AddColumn<string>(
                name: "Gender",
                schema: "product",
                table: "Products",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Unisex");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BodyArea",
                schema: "product",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Gender",
                schema: "product",
                table: "Products");
        }
    }
}
