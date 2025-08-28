using System.ComponentModel.DataAnnotations;

namespace AvicolaRenzoPredictor.Models
{
    public class Prediccion
    {
        public int Id { get; set; }

        [Required]
        public DateTime Fecha { get; set; }

        [Required]
        public double InventarioPromedio { get; set; }

        [Required]
        public double PrecioKg { get; set; }

        [Required]
        public double VentasPred { get; set; }

        [Required]
        public double AbastecerKg { get; set; }

        [Required]
        public string Alerta { get; set; } = string.Empty;

        public string? ModeloVersion { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; }
    }
}

