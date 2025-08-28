namespace AvicolaRenzoPredictor.Models
{
    public class PrediccionOutput
    {
        public double inventario_promedio { get; set; }
        public double precio_kg { get; set; }
        public double ventas_pred { get; set; }
        public double abastecer_kg { get; set; }
        public string alerta { get; set; } = "";
    }
}
