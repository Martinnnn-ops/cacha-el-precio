using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Product_Service.Migrations
{
    /// <inheritdoc />
    public partial class AddProductLayer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Layer",
                schema: "product",
                table: "Products",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "General");

            migrationBuilder.Sql(
                """
                UPDATE product."Products"
                SET "Layer" = CASE
                    WHEN "ProductCategory" LIKE 'Ropa interior%' THEN 'Ropa interior'
                    WHEN "ProductCategory" IN ('Zapatillas', 'Zapatos', 'Botas', 'Sandalias', 'Pantuflas') THEN 'Calzado'
                    WHEN "ProductCategory" IN ('Calcetines', 'Medias') THEN 'Calcetería'
                    WHEN "ProductCategory" IN ('Gorros', 'Jockeys', 'Sombreros', 'Accesorios') THEN 'Accesorio'
                    WHEN "ProductCategory" IN ('Polerones', 'Abrigos', 'Chalecos', 'Chaquetas') THEN 'Abrigo'
                    WHEN "ProductCategory" IN ('Poleras', 'Blusas', 'Camisas') THEN 'Base'
                    WHEN "ProductCategory" IN ('Shorts', 'Faldas', 'Jeans', 'Calzas', 'Pantalones') THEN 'Inferior'
                    WHEN "ProductCategory" IN ('Vestidos', 'Enterizos', 'Overoles', 'Conjuntos', 'Pijamas', 'Trajes de baño') THEN 'Entero'
                    ELSE 'General'
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Layer",
                schema: "product",
                table: "Products");
        }
    }
}
