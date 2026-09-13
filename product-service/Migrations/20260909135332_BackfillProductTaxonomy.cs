using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Product_Service.Migrations
{
    /// <inheritdoc />
    public partial class BackfillProductTaxonomy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE product."Products"
                SET "BodyArea" = CASE
                    WHEN "ProductCategory" IN ('Gorros', 'Jockeys', 'Sombreros') THEN 'Cabeza'
                    WHEN "ProductCategory" IN ('Poleras', 'Polerones', 'Camisas', 'Blusas', 'Chaquetas', 'Abrigos', 'Chalecos') THEN 'Torso'
                    WHEN "ProductCategory" IN ('Pantalones', 'Jeans', 'Calzas', 'Faldas', 'Shorts', 'Ropa interior masculina', 'Ropa interior femenina') THEN 'Piernas'
                    WHEN "ProductCategory" IN ('Zapatillas', 'Zapatos', 'Botas', 'Sandalias', 'Pantuflas', 'Calcetines', 'Medias') THEN 'Pies'
                    WHEN "ProductCategory" IN ('Vestidos', 'Enterizos', 'Overoles', 'Conjuntos', 'Pijamas', 'Trajes de baño') THEN 'Cuerpo completo'
                    ELSE 'Cuerpo'
                END,
                "Gender" = CASE
                    WHEN "ProductCategory" = 'Ropa interior femenina' OR "ProductName" ILIKE ANY (ARRAY['%mujer%', '%femenina%', '%niña%']) THEN 'Mujer'
                    WHEN "ProductCategory" = 'Ropa interior masculina' OR "ProductName" ILIKE ANY (ARRAY['%hombre%', '%masculino%', '%niño%']) THEN 'Hombre'
                    WHEN "ProductName" ILIKE '%unisex%' THEN 'Unisex'
                    ELSE "Gender"
                END;
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Normaliza datos existentes y no puede reconstruir valores
            // anteriores distintos con seguridad.
        }
    }
}
