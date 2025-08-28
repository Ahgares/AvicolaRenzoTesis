# Documento Técnico – AvicolaRenzoBackend

## 1. Resumen
Sistema web para gestión y predicción de inventarios:
- Backend: ASP.NET Core MVC (.NET 9)
- Persistencia: SQLite (EF Core)
- ML: Python 3.10+ con scikit‑learn (modelo serializado `modelo_ventas_simple.pkl`)
- UI: Razor + Bootstrap + Chart.js

## 2. Requisitos y versiones
- .NET SDK: 9.x
- EF Core: 9.x (Sqlite)
- Python: 3.10 o superior
- Paquetes Python: `pandas`, `joblib`, `matplotlib`, `scikit-learn`
- Navegador moderno (se abre automáticamente al iniciar)

## 3. Arquitectura
- `Program.cs` configura servicios, EF Core (SQLite) y rutas. Abre el navegador al iniciar.
- `Data/AppDbContext.cs`: DbContext con `Inventarios` y `Predicciones`.
- `Models/`:
  - `Inventario`, `Prediccion`, `PrediccionOutput` (DTO del predictor)
  - ViewModels: `InventarioFiltroVM`, `PrediccionFiltroVM`, `PrediccionResultadosVM`
- `Controllers/`:
  - `InventarioController`: CRUD básico y carga CSV, export CSV y paginación.
  - `PrediccionController`: filtra datos, ejecuta `predictor.py`, guarda historiales, exporta predicciones y calcula ROP/SS.
- `predictor.py`: carga modelo `modelo_ventas_simple.pkl`, lee CSV de entrada y produce JSON; genera gráfico estático `wwwroot/img/grafico_prediccion.png` (opcional).
- `Views/`: Razor para Inventario y Predicción; gráficos interactivos con Chart.js.

## 4. Flujo de predicción
1) Se filtran registros en `Inventarios` (fecha/mes).
2) Se genera un CSV temporal con columnas: `fecha,inventario_promedio,precio_kg`.
3) ASP.NET ejecuta `predictor.py` (con `python` o `py -3`).
4) Python retorna JSON con `{inventario_promedio, precio_kg, ventas_pred, abastecer_kg, alerta}` y guarda un PNG.
5) C# persiste `Predicciones` y renderiza la vista (gráficos interactivos + recomendaciones).

## 5. ROP / Stock de seguridad
- Parámetros: `ServiceLevel` (default 0.95), `LeadTimeDays` (default 7).
- Cálculos:
  - Demanda diaria: media y desvío de `VentasKg` en el rango.
  - `μ_LT = media_diaria × LT`, `σ_LT = desvío_diario × √LT`.
  - `SS = z × σ_LT`, `ROP = μ_LT + SS` (z por nivel de servicio aproximado).

## 6. Endpoints relevantes
- Inventario
  - `GET /Inventario` (filtros + paginación)
  - `GET /Inventario/Importar`, `POST /Inventario/Importar` (CSV)
  - `GET /Inventario/Plantilla`
  - `GET /Inventario/ExportCsv`
- Predicción
  - `GET /Prediccion/Resultados` (filtros, gráficos y ROP/SS)
  - `GET /Prediccion/Historial` (paginado)
  - `GET /Prediccion/ExportCsvPrediccion`

## 7. Configuración
- `appsettings.json` → `DefaultConnection: Data Source=AvicolaRenzo.db`
- Opcional: `ASPNETCORE_URLS` para puerto/URL. Por defecto `http://localhost:5000`.

## 8. Notas de despliegue
- Windows / Linux: `dotnet publish -c Release`.
- Python disponible en PATH y dependencias instaladas.
- Considerar migraciones EF Core para evolución de esquema (hoy `EnsureCreated`).

## 9. Seguridad y datos
- SQLite local (sin credenciales). Si se publica, proteger archivos `.db` y mover a un RDBMS gestionado.
- No almacenar secretos en el repo; usar variables de entorno.

## 10. Troubleshooting
- “No se pudo ejecutar Python”: confirmar `python --version` o `py -3 --version`; instalar paquetes.
- “Salida inválida del predictor”: revisar columnas/valores del CSV temporal y la existencia del modelo.
- Acentos raros: verificar que los `.cshtml` estén guardados en UTF‑8 y que el navegador no fuerce otra codificación.

## 11. Mapa de archivos clave
- `Program.cs` – arranque y open‑browser.
- `Controllers/InventarioController.cs` – Importar/Exportar CSV.
- `Controllers/PrediccionController.cs` – Predicción/Historial/Export.
- `Models/*` – entidades y ViewModels.
- `Views/Inventario/*.cshtml` – inventario y carga.
- `Views/Prediccion/Historial.cshtml` – historial.
- `predictor.py` – inferencia ML y gráfico.
- `Samples/*.csv` – datos de ejemplo.
