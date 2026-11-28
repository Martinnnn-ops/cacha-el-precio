using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Product_Service.Migrations
{
    /// <inheritdoc />
    public partial class AddEventosAuditoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EventosAuditoria",
                schema: "product",
                columns: table => new
                {
                    EventId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Tipo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    Slug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Nombre = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Store = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    PrecioAnterior = table.Column<int>(type: "integer", nullable: true),
                    PrecioNuevo = table.Column<int>(type: "integer", nullable: true),
                    DescuentoPct = table.Column<int>(type: "integer", nullable: true),
                    Sospechoso = table.Column<bool>(type: "boolean", nullable: false),
                    Motivo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    OcurridoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RecibidoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventosAuditoria", x => x.EventId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EventosAuditoria_CorrelationId",
                schema: "product",
                table: "EventosAuditoria",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_EventosAuditoria_RecibidoEn",
                schema: "product",
                table: "EventosAuditoria",
                column: "RecibidoEn");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventosAuditoria",
                schema: "product");
        }
    }
}
