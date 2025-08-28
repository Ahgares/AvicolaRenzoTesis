using System.ComponentModel.DataAnnotations;

namespace AvicolaRenzoPredictor.Models
{
    public class Inventario
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public DateTime Fecha { get; set; }

        [Required]
        public float InventarioPromedio { get; set; }

        [Required]
        public float PrecioKg { get; set; }

        [Required]
        public float VentasKg { get; set; }

        [Required]
        public float PerdidasKg { get; set; }

        [Required]
        public string Observacion { get; set; } = string.Empty;
    }
}


