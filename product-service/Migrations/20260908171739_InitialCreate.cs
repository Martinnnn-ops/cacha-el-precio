using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Product_Service.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProductName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    ProductBrand = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    ProductPrice = table.Column<int>(type: "INTEGER", nullable: false),
                    XS = table.Column<bool>(type: "INTEGER", nullable: false),
                    S = table.Column<bool>(type: "INTEGER", nullable: false),
                    M = table.Column<bool>(type: "INTEGER", nullable: false),
                    L = table.Column<bool>(type: "INTEGER", nullable: false),
                    XL = table.Column<bool>(type: "INTEGER", nullable: false),
                    XXL = table.Column<bool>(type: "INTEGER", nullable: false),
                    ProductCategory_OriginalValue = table.Column<string>(type: "TEXT", nullable: false),
                    ProductCategory_Type = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.ProductId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Products");
        }
    }
}
