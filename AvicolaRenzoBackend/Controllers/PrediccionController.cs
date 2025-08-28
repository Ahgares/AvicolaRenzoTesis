using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AvicolaRenzoPredictor.Data;
using AvicolaRenzoPredictor.Models;

namespace AvicolaRenzoPredictor.Controllers
{
    public class PrediccionController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<PrediccionController> _logger;

        public PrediccionController(AppDbContext context, IWebHostEnvironment env, ILogger<PrediccionController> logger)
        {
            _context = context;
            _env = env;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Resultados([FromQuery] PrediccionFiltroVM filtro)
        {
            // 1) Tomar datos segÃƒÂºn filtros para la predicciÃƒÂ³n
            var q = _context.Inventarios.AsQueryable();
            var tieneFiltros = false;
            if (filtro.Desde.HasValue)
            {
                q = q.Where(i => i.Fecha >= filtro.Desde.Value.Date);
                tieneFiltros = true;
            }
            if (filtro.Hasta.HasValue)
            {
                q = q.Where(i => i.Fecha <= filtro.Hasta.Value.Date);
                tieneFiltros = true;
            }
            if (filtro.Mes.HasValue)
            {
                var m = filtro.Mes.Value;
                q = q.Where(i => i.Fecha.Month == m);
                tieneFiltros = true;
            }

            q = q.OrderBy(i => i.Fecha);
            if (!tieneFiltros)
            {
                q = q.Take(100);
            }

            var datos = await q.ToListAsync();

            if (!datos.Any())
            {
                TempData["Error"] = "No hay datos de inventario que coincidan con los filtros.";
                return RedirectToAction("Index", "Inventario");
            }

            // 2) Generar CSV temporal con columnas esperadas por predictor.py
            var tempCsv = Path.Combine(Path.GetTempPath(), $"pred_input_{Guid.NewGuid():N}.csv");
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("fecha,inventario_promedio,precio_kg");
                foreach (var i in datos)
                {
                    sb.AppendLine($"{i.Fecha:yyyy-MM-dd},{i.InventarioPromedio.ToString(CultureInfo.InvariantCulture)},{i.PrecioKg.ToString(CultureInfo.InvariantCulture)}");
                }
                await System.IO.File.WriteAllTextAsync(tempCsv, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generando CSV temporal");
                TempData["Error"] = "No se pudo generar el archivo de entrada para la predicciÃƒÂ³n.";
                return RedirectToAction("Index", "Inventario");
            }

            // 3) Ejecutar predictor.py
            var workDir = _env.ContentRootPath; // contiene predictor.py y modelo_ventas_simple.pkl
            var chartMode = string.IsNullOrWhiteSpace(filtro.ChartMode) ? "compare" : filtro.ChartMode.Trim().ToLower();
            if (chartMode != "single" && chartMode != "compare") chartMode = "compare";
            var (ok, jsonSalida, err) = await EjecutarPythonAsync(workDir, "predictor.py", tempCsv, chartMode);

            try { System.IO.File.Delete(tempCsv); } catch { /* ignore */ }

            if (!ok)
            {
                _logger.LogError("Error ejecutando predictor.py: {err}", err);
                TempData["Error"] = "Error al ejecutar el Predictor (ver logs). AsegÃƒÂºrate de tener Python y dependencias instaladas.";
                return RedirectToAction("Index", "Inventario");
            }

            // 4) Parsear JSON de salida
            try
            {
                var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var lista = JsonSerializer.Deserialize<List<PrediccionOutput>>(jsonSalida, opts) ?? new List<PrediccionOutput>();

                // Guardar predicciones en BD con timestamp y versiÃƒÂ³n de modelo opcional
                var ahora = DateTime.UtcNow;
                var preds = lista.Select(x => new Prediccion
                {
                    Fecha = DateTime.UtcNow.Date, // sin fecha por item, usamos corrida actual
                    InventarioPromedio = x.inventario_promedio,
                    PrecioKg = x.precio_kg,
                    VentasPred = x.ventas_pred,
                    AbastecerKg = x.abastecer_kg,
                    Alerta = x.alerta,
                    ModeloVersion = "modelo_ventas_simple.pkl",
                    CreatedAt = ahora
                }).ToList();

                await _context.Predicciones.AddRangeAsync(preds);
                await _context.SaveChangesAsync();

                // 5) Calcular mÃƒÂ©tricas histÃƒÂ³ricas para recomendaciones
                var refDate = filtro.Hasta?.Date ?? DateTime.Today;
                var mesObjetivo = filtro.Mes ?? refDate.Month;
                var cultura = CultureInfo.GetCultureInfo("es-PE");
                string mesNombre = cultura.DateTimeFormat.GetMonthName(mesObjetivo);

                // ÃƒÅ¡ltimos 3 meses (respecto a refDate)
                var desde3 = refDate.AddMonths(-3);
                var ult3 = await _context.Inventarios
                    .Where(i => i.Fecha >= desde3 && i.Fecha <= refDate)
                    .Select(i => new { i.VentasKg, i.PerdidasKg })
                    .ToListAsync();
                var ult3Ventas = Math.Round(ult3.Sum(x => (double)x.VentasKg), 2);
                var perdPromUlt3 = ult3.Count > 0 ? Math.Round(ult3.Average(x => (double)x.PerdidasKg), 2) : 0;

                // Mismo mes del aÃƒÂ±o anterior
                var anioAnterior = refDate.Year - 1;
                var mismoMesAnioAnterior = await _context.Inventarios
                    .Where(i => i.Fecha.Year == anioAnterior && i.Fecha.Month == mesObjetivo)
                    .Select(i => new { i.VentasKg })
                    .ToListAsync();
                var ventasMesAnteriorAnio = Math.Round(mismoMesAnioAnterior.Sum(x => (double)x.VentasKg), 2);

                // Armar recomendaciones
                var recomendaciones = new List<string>();
                var promPred = lista.Any() ? Math.Round(lista.Average(x => x.ventas_pred), 2) : 0;
                var totalPred = Math.Round(lista.Sum(x => x.ventas_pred), 2);
                var totalInv = Math.Round(lista.Sum(x => x.inventario_promedio), 2);
                var gap = Math.Round(totalPred - totalInv, 2);

                // Sugerencia de abastecimiento con 10% de colchÃƒÂ³n + pÃƒÂ©rdidas promedio de los ÃƒÂºltimos 3 meses
                var safety = 0.10;
                var extraPorPerdidas = Math.Round(perdPromUlt3 * Math.Max(1, lista.Count), 2);
                var sugerirAbastecer = gap > 0 ? Math.Ceiling(gap * (1 + safety) + extraPorPerdidas) : 0;
                var excesoEsperado = gap < 0 ? Math.Ceiling(Math.Abs(gap)) : 0;

                recomendaciones.Add($"ÃƒÅ¡ltimos 3 meses: {ult3Ventas:N2} kg vendidos; pÃƒÂ©rdidas promedio {perdPromUlt3:N2} kg/dÃƒÂ­a.");
                recomendaciones.Add($"{mesNombre} {anioAnterior}: {ventasMesAnteriorAnio:N2} kg vendidos.");

                if (filtro.Mes.HasValue)
                {
                    recomendaciones.Add($"PredicciÃƒÂ³n actual para {mesNombre}: promedio {promPred:N2} kg por registro; total estimado en conjunto {totalPred:N2} kg.");
                }
                else
                {
                    recomendaciones.Add($"PredicciÃƒÂ³n promedio actual: {promPred:N2} kg por registro; total en conjunto {totalPred:N2} kg.");
                }

                if (sugerirAbastecer > 0)
                {
                    recomendaciones.Add($"RecomendaciÃƒÂ³n: abastecer al menos {sugerirAbastecer:N0} kg para cubrir demanda (+10% colchÃƒÂ³n y pÃƒÂ©rdidas). Considera programar compras escalonadas.");
                }
                else if (excesoEsperado > 0)
                {
                    recomendaciones.Add($"RecomendaciÃƒÂ³n: se estima un excedente de {excesoEsperado:N0} kg; prioriza rotaciÃƒÂ³n, evalÃƒÂºa promociones o reduce compras esta ventana.");
                }

                // Series para grÃƒÂ¡ficos
                var labels = datos.Select(d => d.Fecha.ToString("yyyy-MM-dd")).ToList();
                var serieInv = datos.Select(d => (double)d.InventarioPromedio).ToList();
                var seriePrecio = datos.Select(d => (double)d.PrecioKg).ToList();

                var vm = new PrediccionResultadosVM
                {
                    Filtro = filtro,
                    Resultados = lista,
                    TotalFilas = lista.Count,
                    PromedioInventario = lista.Any() ? Math.Round(lista.Average(x => x.inventario_promedio), 2) : 0,
                    PromedioPrecio = lista.Any() ? Math.Round(lista.Average(x => x.precio_kg), 2) : 0,
                    PromedioVentasPred = lista.Any() ? Math.Round(lista.Average(x => x.ventas_pred), 2) : 0,
                    Ultimos3MesesVentas = ult3Ventas,
                    VentasMismoMesAnioAnterior = ventasMesAnteriorAnio,
                    MesObjetivoNombre = mesNombre,
                    AnioAnteriorReferencia = anioAnterior,
                    Recomendaciones = recomendaciones,
                    SugerenciaAbastecerKg = sugerirAbastecer,
                    ExcesoEsperadoKg = excesoEsperado,
                    PerdidaPromUlt3Meses = perdPromUlt3,
                    Labels = labels,
                    SerieInventario = serieInv,
                    SeriePrecio = seriePrecio
                };

                // 6) PolÃƒÂ­tica de reabastecimiento (ROP/SS) a partir de ventas histÃƒÂ³ricas diarias en el rango filtrado
                var rangoQ = _context.Inventarios.AsQueryable();
                if (filtro.Desde.HasValue) rangoQ = rangoQ.Where(i => i.Fecha >= filtro.Desde.Value.Date);
                if (filtro.Hasta.HasValue) rangoQ = rangoQ.Where(i => i.Fecha <= filtro.Hasta.Value.Date);
                if (filtro.Mes.HasValue) rangoQ = rangoQ.Where(i => i.Fecha.Month == filtro.Mes.Value);
                var ventasHist = await rangoQ.Select(i => i.VentasKg).ToListAsync();
                double dailyAvg = ventasHist.Any() ? ventasHist.Average(v => (double)v) : 0.0;
                double dailyStd = 0.0;
                if (ventasHist.Count > 1)
                {
                    var mean = dailyAvg;
                    dailyStd = Math.Sqrt(ventasHist.Average(v => Math.Pow((double)v - mean, 2)));
                }
                double service = filtro.ServiceLevel ?? 0.95;
                int lt = filtro.LeadTimeDays ?? 7;
                double z = ZFromService(service);
                double muLT = dailyAvg * lt;
                double sigmaLT = dailyStd * Math.Sqrt(lt);
                double ss = z * sigmaLT;
                double rop = muLT + ss;

                vm.ServiceLevel = service;
                vm.LeadTimeDays = lt;
                vm.DailyAvg = Math.Round(dailyAvg, 2);
                vm.DailyStd = Math.Round(dailyStd, 2);
                vm.SafetyStock = Math.Round(ss, 2);
                vm.ROP = Math.Round(rop, 2);

                return View("~/Views/Inventario/Resultado.cshtml", vm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "JSON invÃƒÂ¡lido desde predictor.py. Respuesta: {json}", jsonSalida);
                TempData["Error"] = "Salida invÃƒÂ¡lida del predictor. Revisa el CSV y el modelo.";
                return RedirectToAction("Index", "Inventario");
            }
        }

        [HttpGet]
        public async Task<IActionResult> Historial()
        {
            int page = 1;
            int pageSize = 50;
            if (Request.Query.ContainsKey("page")) int.TryParse(Request.Query["page"], out page);
            if (Request.Query.ContainsKey("pageSize")) int.TryParse(Request.Query["pageSize"], out pageSize);
            if (page <= 0) page = 1; if (pageSize <= 0) pageSize = 50;

            var baseQ = _context.Predicciones.AsQueryable();
            var total = await baseQ.CountAsync();
            var ultimas = await baseQ
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.Total = total;
            ViewBag.TotalPages = Math.Max(1, (int)Math.Ceiling((double)total / pageSize));
            return View("~/Views/Prediccion/Historial.cshtml", ultimas);
        }

        [HttpGet]
        public async Task<IActionResult> ExportCsvPrediccion([FromQuery] PrediccionFiltroVM filtro)
        {
            // Reutiliza la lÃƒÂ³gica de Resultados para obtener 'datos' y ejecutar el predictor
            var q = _context.Inventarios.AsQueryable();
            if (filtro.Desde.HasValue) q = q.Where(i => i.Fecha >= filtro.Desde.Value.Date);
            if (filtro.Hasta.HasValue) q = q.Where(i => i.Fecha <= filtro.Hasta.Value.Date);
            if (filtro.Mes.HasValue) q = q.Where(i => i.Fecha.Month == filtro.Mes.Value);

            var datos = await q.OrderBy(i => i.Fecha).ToListAsync();
            if (!datos.Any())
                return File(System.Text.Encoding.UTF8.GetBytes(""), "text/csv", "predicciones.csv");

            var tempCsv = Path.Combine(Path.GetTempPath(), $"pred_input_{Guid.NewGuid():N}.csv");
            var sb = new StringBuilder();
            sb.AppendLine("fecha,inventario_promedio,precio_kg");
            foreach (var i in datos)
                sb.AppendLine($"{i.Fecha:yyyy-MM-dd},{i.InventarioPromedio.ToString(System.Globalization.CultureInfo.InvariantCulture)},{i.PrecioKg.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            await System.IO.File.WriteAllTextAsync(tempCsv, sb.ToString(), Encoding.UTF8);

            var chartMode2 = string.IsNullOrWhiteSpace(filtro.ChartMode) ? "compare" : filtro.ChartMode.Trim().ToLower();
            if (chartMode2 != "single" && chartMode2 != "compare") chartMode2 = "compare";
            var (ok, jsonSalida, err) = await EjecutarPythonAsync(_env.ContentRootPath, "predictor.py", tempCsv, chartMode2);
            try { System.IO.File.Delete(tempCsv); } catch { }
            if (!ok) return File(System.Text.Encoding.UTF8.GetBytes($"error,{err}"), "text/csv", "predicciones_error.csv");

            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var lista = JsonSerializer.Deserialize<List<PrediccionOutput>>(jsonSalida, opts) ?? new List<PrediccionOutput>();

            var sbOut = new StringBuilder();
            sbOut.AppendLine("inventario_promedio,precio_kg,ventas_pred,abastecer_kg,alerta");
            foreach (var x in lista)
                sbOut.AppendLine($"{x.inventario_promedio},{x.precio_kg},{x.ventas_pred},{x.abastecer_kg},\"{x.alerta.Replace("\"", "''")}\"");
            var bytes = System.Text.Encoding.UTF8.GetBytes(sbOut.ToString());
            return File(bytes, "text/csv", $"predicciones_{DateTime.Now:yyyyMMdd_HHmm}.csv");
        }

        private static async Task<(bool ok, string stdout, string stderr)> EjecutarPythonAsync(string workingDirectory, string scriptName, string csvPath, string? extraArg = null)
        {
            // Intentar con "python" y luego con "py -3"
            var intentos = new List<(string file, string args)>
            {
                ("python", $"{scriptName} \"{csvPath}\"{(extraArg!=null?" "+extraArg:"")}"),
                ("py", $"-3 {scriptName} \"{csvPath}\"{(extraArg!=null?" "+extraArg:"")}")
            };

            foreach (var intento in intentos)
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = intento.file,
                        Arguments = intento.args,
                        WorkingDirectory = workingDirectory,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    };

                    using var proc = new Process { StartInfo = psi };
                    var sbOut = new StringBuilder();
                    var sbErr = new StringBuilder();

                    proc.OutputDataReceived += (s, e) => { if (e.Data != null) sbOut.AppendLine(e.Data); };
                    proc.ErrorDataReceived += (s, e) => { if (e.Data != null) sbErr.AppendLine(e.Data); };

                    proc.Start();
                    proc.BeginOutputReadLine();
                    proc.BeginErrorReadLine();
                    await proc.WaitForExitAsync();

                    var stdout = sbOut.ToString().Trim();
                    var stderr = sbErr.ToString().Trim();

                    if (proc.ExitCode == 0 && !string.IsNullOrWhiteSpace(stdout))
                    {
                        return (true, stdout, stderr);
                    }
                    // si falla, probar siguiente intento
                }
                catch
                {
                    // ignorar y probar siguiente
                }
            }

            return (false, string.Empty, "No se pudo ejecutar Python (probÃƒÂ© 'python' y 'py -3')");
        }

        private static double ZFromService(double p)
        {
            // Aprox. inversa de normal estÃƒÂ¡ndar por segmentos comunes
            if (p >= 0.999) return 3.09;
            if (p >= 0.99) return 2.33;
            if (p >= 0.98) return 2.05;
            if (p >= 0.975) return 1.96;
            if (p >= 0.95) return 1.645;
            if (p >= 0.90) return 1.282;
            if (p >= 0.85) return 1.036;
            if (p >= 0.80) return 0.842;
            if (p <= 0.5) return 0.0;
            return 1.0; // por defecto
        }
    }
}






