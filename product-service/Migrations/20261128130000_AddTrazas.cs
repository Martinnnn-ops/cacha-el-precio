using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Product_Service.Migrations
{
    /// <inheritdoc />
    public partial class AddTrazas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Trazas",
                schema: "product",
                columns: table => new
                {
                    CorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Etapa = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Cantidad = table.Column<long>(type: "bigint", nullable: false),
                    PrimeraVez = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UltimaVez = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trazas", x => new { x.CorrelationId, x.Etapa });
                });

            migrationBuilder.CreateIndex(
                name: "IX_Trazas_UltimaVez",
                schema: "product",
                table: "Trazas",
                column: "UltimaVez");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Trazas",
                schema: "product");
        }
    }
}
