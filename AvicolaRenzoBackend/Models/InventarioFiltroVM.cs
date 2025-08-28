using System.ComponentModel.DataAnnotations;

namespace AvicolaRenzoPredictor.Models
{
    public class InventarioFiltroVM
    {
        [DataType(DataType.Date)]
        public DateTime? Desde { get; set; }

        [DataType(DataType.Date)]
        public DateTime? Hasta { get; set; }

        public float? PrecioMin { get; set; }
        public float? PrecioMax { get; set; }

        public string? Observacion { get; set; }

        // Resultados
        public List<Inventario> Resultados { get; set; } = new();

        // Paginación
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
        public int Total { get; set; }
        public int TotalPages => PageSize > 0 ? Math.Max(1, (int)Math.Ceiling((double)Total / PageSize)) : 1;
    }
}
