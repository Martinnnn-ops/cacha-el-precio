using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Product_Service.Migrations
{
    /// <summary>
    /// Cada precio capturado (Kinesis o relleno inicial), para el gráfico de la
    /// ficha. Ver <c>ObservacionPrecio</c>.
    /// </summary>
    public partial class AddObservacionesPrecio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ObservacionesPrecio",
                schema: "product",
                columns: table => new
                {
                    Store = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CapturadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Precio = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ObservacionesPrecio", x => new { x.Store, x.ExternalId, x.CapturadoEn });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ObservacionesPrecio",
                schema: "product");
        }
    }
}
