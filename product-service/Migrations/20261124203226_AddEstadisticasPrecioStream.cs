using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Product_Service.Migrations
{
    /// <inheritdoc />
    public partial class AddEstadisticasPrecioStream : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CheckpointsStream",
                schema: "product",
                columns: table => new
                {
                    StreamShard = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    NumeroSecuencia = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ActualizadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckpointsStream", x => x.StreamShard);
                });

            migrationBuilder.CreateTable(
                name: "EstadisticasPrecio",
                schema: "product",
                columns: table => new
                {
                    Store = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Muestras = table.Column<long>(type: "bigint", nullable: false),
                    Suma = table.Column<long>(type: "bigint", nullable: false),
                    SumaCuadrados = table.Column<decimal>(type: "numeric(29,0)", precision: 29, scale: 0, nullable: false),
                    Minimo = table.Column<int>(type: "integer", nullable: false),
                    Maximo = table.Column<int>(type: "integer", nullable: false),
                    UltimoPrecio = table.Column<int>(type: "integer", nullable: false),
                    UltimaCaptura = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EstadisticasPrecio", x => new { x.Store, x.ExternalId });
                });

            migrationBuilder.CreateTable(
                name: "EventosPrecioProcesados",
                schema: "product",
                columns: table => new
                {
                    EventId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProcesadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventosPrecioProcesados", x => x.EventId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EventosPrecioProcesados_ProcesadoEn",
                schema: "product",
                table: "EventosPrecioProcesados",
                column: "ProcesadoEn");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CheckpointsStream",
                schema: "product");

            migrationBuilder.DropTable(
                name: "EstadisticasPrecio",
                schema: "product");

            migrationBuilder.DropTable(
                name: "EventosPrecioProcesados",
                schema: "product");
        }
    }
}
