using System.ComponentModel.DataAnnotations;

namespace AvicolaRenzoPredictor.Models
{
    public class PrediccionResultadosVM
    {
        public PrediccionFiltroVM Filtro { get; set; } = new();
        public List<PrediccionOutput> Resultados { get; set; } = new();

        // Resumen
        public int TotalFilas { get; set; }
        public double PromedioInventario { get; set; }
        public double PromedioPrecio { get; set; }
        public double PromedioVentasPred { get; set; }

        // Métricas históricas para recomendaciones
        public double Ultimos3MesesVentas { get; set; }
        public double VentasMismoMesAnioAnterior { get; set; }
        public string MesObjetivoNombre { get; set; } = string.Empty;
        public int AnioAnteriorReferencia { get; set; }

        // Recomendaciones armadas
        public List<string> Recomendaciones { get; set; } = new();

        // Sugerencias cuantitativas
        public double SugerenciaAbastecerKg { get; set; }
        public double ExcesoEsperadoKg { get; set; }
        public double PerdidaPromUlt3Meses { get; set; }

        // Política ROP/Stock de seguridad
        public double ServiceLevel { get; set; }
        public int LeadTimeDays { get; set; }
        public double DailyAvg { get; set; }
        public double DailyStd { get; set; }
        public double SafetyStock { get; set; }
        public double ROP { get; set; }

        // Series para gráficos interactivos
        public List<string> Labels { get; set; } = new();
        public List<double> SerieInventario { get; set; } = new();
        public List<double> SeriePrecio { get; set; } = new();
    }
}
