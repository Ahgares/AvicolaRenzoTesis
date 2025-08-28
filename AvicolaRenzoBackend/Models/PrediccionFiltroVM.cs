using System.ComponentModel.DataAnnotations;

namespace AvicolaRenzoPredictor.Models
{
    public class PrediccionFiltroVM
    {
        [DataType(DataType.Date)]
        public DateTime? Desde { get; set; }

        [DataType(DataType.Date)]
        public DateTime? Hasta { get; set; }

        [Range(1, 12)]
        public int? Mes { get; set; }

        // Parámetros de política de reabastecimiento
        [Range(0.5, 0.999)]
        public double? ServiceLevel { get; set; } = 0.95; // 95%

        [Range(1, 60)]
        public int? LeadTimeDays { get; set; } = 7;

        // Modo de gráfico: "compare" (predicción vs inventario) o "single" (solo predicción)
        public string? ChartMode { get; set; } = "compare";
    }
}
