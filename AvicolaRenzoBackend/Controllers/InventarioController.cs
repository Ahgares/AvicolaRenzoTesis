using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using AvicolaRenzoPredictor.Data;
using AvicolaRenzoPredictor.Models;
using Microsoft.EntityFrameworkCore;

namespace AvicolaRenzoPredictor.Controllers
{
    public class InventarioController : Controller
    {
        private readonly AppDbContext _context;

        public InventarioController(AppDbContext context)
        {
            _context = context;
        }

        // ---------- EXISTENTES ----------
        public async Task<IActionResult> Index([FromQuery] InventarioFiltroVM filtro)
        {
            var q = _context.Inventarios.AsQueryable();

            if (filtro.Desde.HasValue)
            {
                var d = filtro.Desde.Value.Date;
                q = q.Where(i => i.Fecha >= d);
            }
            if (filtro.Hasta.HasValue)
            {
                var h = filtro.Hasta.Value.Date;
                q = q.Where(i => i.Fecha <= h);
            }
            if (filtro.PrecioMin.HasValue)
            {
                q = q.Where(i => i.PrecioKg >= filtro.PrecioMin.Value);
            }
            if (filtro.PrecioMax.HasValue)
            {
                q = q.Where(i => i.PrecioKg <= filtro.PrecioMax.Value);
            }
            if (!string.IsNullOrWhiteSpace(filtro.Observacion))
            {
                var txt = filtro.Observacion.Trim();
                q = q.Where(i => i.Observacion.Contains(txt));
            }

            q = q.OrderByDescending(i => i.Fecha);

            // Total antes de paginar
            filtro.Total = await q.CountAsync();

            // PaginaciÃ³n
            if (filtro.PageSize <= 0) filtro.PageSize = 50;
            if (filtro.Page <= 0) filtro.Page = 1;
            q = q.Skip((filtro.Page - 1) * filtro.PageSize).Take(filtro.PageSize);

            filtro.Resultados = await q.ToListAsync();
            return View(filtro);
        }

        [HttpGet]
        public async Task<IActionResult> ExportCsv([FromQuery] InventarioFiltroVM filtro)
        {
            var q = _context.Inventarios.AsQueryable();
            if (filtro.Desde.HasValue) q = q.Where(i => i.Fecha >= filtro.Desde.Value.Date);
            if (filtro.Hasta.HasValue) q = q.Where(i => i.Fecha <= filtro.Hasta.Value.Date);
            if (filtro.PrecioMin.HasValue) q = q.Where(i => i.PrecioKg >= filtro.PrecioMin.Value);
            if (filtro.PrecioMax.HasValue) q = q.Where(i => i.PrecioKg <= filtro.PrecioMax.Value);
            if (!string.IsNullOrWhiteSpace(filtro.Observacion)) q = q.Where(i => i.Observacion.Contains(filtro.Observacion.Trim()));

            var datos = await q.OrderBy(i => i.Fecha).ToListAsync();
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Fecha,InventarioPromedio,PrecioKg,VentasKg,PerdidasKg,Observacion");
            foreach (var i in datos)
            {
                sb.AppendLine($"{i.Fecha:yyyy-MM-dd},{i.InventarioPromedio},{i.PrecioKg},{i.VentasKg},{i.PerdidasKg},\"{i.Observacion.Replace("\"", "''")}\"");
            }
            var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/csv", $"inventario_filtrado_{DateTime.Now:yyyyMMdd_HHmm}.csv");
        }

        [HttpGet]
        public IActionResult Agregar() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Agregar(Inventario inventario)
        {
            if (!ModelState.IsValid) return View(inventario);

            _context.Add(inventario);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // ---------- NUEVO: DESCARGA PLANTILLA ----------
        [HttpGet]
        public IActionResult Plantilla()
        {
            var csv = "Fecha,InventarioPromedio,PrecioKg,VentasKg,PerdidasKg,Observacion\r\n" +
                      "2025-07-01,180,10.5,165,2,DÃ­a normal\r\n" +
                      "2025-07-02,200,10.2,175,1,PromociÃ³n local\r\n";

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
            return File(bytes, "text/csv", "plantilla_inventario.csv");
        }

        // ---------- NUEVO: FORMULARIO DE IMPORTACIÃ“N ----------
        [HttpGet]
        public IActionResult Importar()
        {
            return View();
        }

        // ---------- NUEVO: PROCESA CSV ----------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Importar(IFormFile archivo)
        {
            if (archivo == null || archivo.Length == 0)
            {
                TempData["Error"] = "Selecciona un archivo CSV.";
                return View();
            }

            try
            {
                // Leemos todo para poder detectar el delimitador (, o ;)
                string contenido;
                using (var sr = new StreamReader(archivo.OpenReadStream()))
                    contenido = await sr.ReadToEndAsync();

                var delimitador = contenido.Count(c => c == ';') > contenido.Count(c => c == ',') ? ";" : ",";

                using var reader = new StringReader(contenido);
                var cfg = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = true,
                    Delimiter = delimitador,
                    TrimOptions = TrimOptions.Trim | TrimOptions.InsideQuotes,
                    PrepareHeaderForMatch = args => args.Header?.Trim().ToLower().Replace(" ", "") ?? ""
                };

                using var csv = new CsvReader(reader, cfg);

                // DTO intermedio para tolerar encabezados
                var registros = new List<InventarioCsv>();
                await foreach (var r in csv.GetRecordsAsync<InventarioCsv>())
                {
                    registros.Add(r);
                }

                if (!registros.Any())
                {
                    TempData["Error"] = "El CSV estÃ¡ vacÃ­o o no tiene columnas vÃ¡lidas.";
                    return View();
                }

                var filas = new List<Inventario>();
                var errores = new List<string>();
                int linea = 1; // sin contar encabezado
                foreach (var r in registros)
                {
                    linea++;
                    // Normalizaciones y parseos tolerantes
                    var fecha = ParseFecha(r.Fecha);
                    var inv = ParseDecimal(r.InventarioPromedio);
                    var p = ParseDecimal(r.PrecioKg);
                    var v = ParseDecimal(r.VentasKg);
                    var perd = ParseDecimal(r.PerdidasKg);

                    if (fecha == null || inv == null || p == null || v == null || perd == null)
                    {
                        var motivos = new List<string>();
                        if (fecha == null) motivos.Add("Fecha invÃ¡lida");
                        if (inv == null) motivos.Add("InventarioPromedio invÃ¡lido");
                        if (p == null) motivos.Add("PrecioKg invÃ¡lido");
                        if (v == null) motivos.Add("VentasKg invÃ¡lido");
                        if (perd == null) motivos.Add("PerdidasKg invÃ¡lido");
                        errores.Add($"LÃ­nea {linea}: {string.Join(", ", motivos)}");
                        continue;
                    }

                    filas.Add(new Inventario
                    {
                        Fecha = fecha.Value,
                        InventarioPromedio = (float)Math.Round((double)inv.Value, 0),
                        PrecioKg = (float)Math.Round((double)p.Value, 2),
                        VentasKg = (float)Math.Round((double)v.Value, 0),
                        PerdidasKg = (float)Math.Round((double)perd.Value, 0),
                        Observacion = r.Observacion ?? ""
                    });

                }

                if (!filas.Any())
                {
                    TempData["Error"] = "No se pudo convertir ninguna fila. Revisa los formatos.";
                    return View();
                }

                await _context.Inventarios.AddRangeAsync(filas);
                await _context.SaveChangesAsync();

                TempData["Ok"] = $"Se importaron {filas.Count} filas correctamente.";
                if (errores.Any())
                {
                    var preview = string.Join(" | ", errores.Take(8));
                    TempData["Warn"] = $"Se omitieron {errores.Count} filas: {preview}{(errores.Count>8?" ...":"")}";
                }
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error procesando CSV: " + ex.Message;
                return View();
            }
        }

        // ---------- Helpers internos ----------
        private static DateTime? ParseFecha(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            var formatos = new[] { "yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "MM/dd/yyyy" };
            foreach (var f in formatos)
                if (DateTime.TryParseExact(s.Trim(), f, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                    return dt;
            // Ãšltimo intento con parse estÃ¡ndar
            if (DateTime.TryParse(s, new CultureInfo("es-PE"), DateTimeStyles.None, out var dte)) return dte;
            return null;
        }

        private static decimal? ParseDecimal(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            s = s.Trim().Replace(" ", "");
            // normaliza coma a punto
            s = s.Replace(",", ".");
            if (decimal.TryParse(s, NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var val))
                return val;
            return null;
        }

        // Clase DTO para lectura flexible del CSV
        private class InventarioCsv
        {
            public string? Fecha { get; set; }
            public string? InventarioPromedio { get; set; }
            public string? PrecioKg { get; set; }
            public string? VentasKg { get; set; }
            public string? PerdidasKg { get; set; }
            public string? Observacion { get; set; }
        }
    }
}

