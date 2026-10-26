using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Notification_Service.Migrations
{
    /// <inheritdoc />
    public partial class BandejaCorreos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "correos_generados",
                schema: "notification",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    para = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    asunto = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    html = table.Column<string>(type: "text", nullable: false),
                    texto = table.Column<string>(type: "text", nullable: false),
                    modo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    generado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_correos_generados", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "correos_generados",
                schema: "notification");
        }
    }
}
