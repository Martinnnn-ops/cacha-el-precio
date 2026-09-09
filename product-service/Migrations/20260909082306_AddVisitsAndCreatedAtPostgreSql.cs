using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Product_Service.Migrations
{
    /// <inheritdoc />
    public partial class AddVisitsAndCreatedAtPostgreSql : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "product",
                table: "Products",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<int>(
                name: "Visits",
                schema: "product",
                table: "Products",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Products_CreatedAt",
                schema: "product",
                table: "Products",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Visits",
                schema: "product",
                table: "Products",
                column: "Visits");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_CreatedAt",
                schema: "product",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_Visits",
                schema: "product",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "product",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Visits",
                schema: "product",
                table: "Products");
        }
    }
}
