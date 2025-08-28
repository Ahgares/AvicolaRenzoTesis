using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AvicolaRenzoPredictor.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Inventarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Fecha = table.Column<DateTime>(type: "TEXT", nullable: false),
                    InventarioPromedio = table.Column<double>(type: "REAL", nullable: false),
                    PrecioKg = table.Column<double>(type: "REAL", nullable: false),
                    VentasKg = table.Column<double>(type: "REAL", nullable: false),
                    PerdidasKg = table.Column<double>(type: "REAL", nullable: false),
                    Observacion = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inventarios", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Inventarios");
        }
    }
}
