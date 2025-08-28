using Microsoft.EntityFrameworkCore;
using AvicolaRenzoPredictor.Models;

namespace AvicolaRenzoPredictor.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // Tabla de Inventarios
        public DbSet<Inventario> Inventarios { get; set; }

        // Tabla de Predicciones
        public DbSet<Prediccion> Predicciones { get; set; }

        // Configuración de entidades
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Inventario>().ToTable("Inventarios");
            modelBuilder.Entity<Inventario>().HasKey(i => i.Id);
        }
    }
}
