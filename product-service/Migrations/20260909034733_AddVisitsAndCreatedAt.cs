using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Product_Service.Migrations
{
    /// <inheritdoc />
    public partial class AddVisitsAndCreatedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Products",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "Visits",
                table: "Products",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // Los productos que ya estaban no tienen fecha de alta, porque hasta
            // ahora nadie la guardaba. EF rellena la columna nueva con el valor
            // por defecto del tipo —el año 1—, y la ficha traduciría eso a
            // «agregado hace 739.000 días».
            //
            // Se les pone la fecha de esta migración: no es cuándo entraron de
            // verdad, pero sí lo único que consta —existían para entonces— y
            // deja «Lo más reciente» ordenando de forma utilizable desde el
            // primer día. La comparación va contra 1900 y no contra la fecha
            // exacta para no depender de cómo SQLite haya escrito el texto.
            migrationBuilder.Sql(
                "UPDATE \"Products\" SET \"CreatedAt\" = CURRENT_TIMESTAMP " +
                "WHERE \"CreatedAt\" < '1900-01-01';");

            migrationBuilder.CreateIndex(
                name: "IX_Products_CreatedAt",
                table: "Products",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Visits",
                table: "Products",
                column: "Visits");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_CreatedAt",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_Visits",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Visits",
                table: "Products");
        }
    }
}
