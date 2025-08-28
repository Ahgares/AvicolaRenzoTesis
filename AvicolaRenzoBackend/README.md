# AvicolaRenzoBackend

Aplicación ASP.NET Core MVC + Python para análisis y predicción de inventarios de Avícola Renzo.

## Características
- Carga de inventario manual o vía CSV.
- Predicción de ventas con modelo Python (`modelo_ventas_simple.pkl`).
- Gráficos interactivos (Chart.js) y recomendaciones automáticas.
- Historial de predicciones y exportación CSV.
- ROP/Stock de seguridad configurable (nivel de servicio y lead time).

## Requisitos
- .NET SDK 9.x
- Python 3.10+ con paquetes: `pandas`, `joblib`, `matplotlib`, `scikit-learn`.
- Git (para clonar/compartir).

## Ejecución local
```powershell
# 1) Restaurar dependencias .NET (opcional)
dotnet restore

# 2) Instalar dependencias Python (si hace falta)
python -m pip install -U pandas joblib matplotlib scikit-learn

# 3) Ejecutar la app
dotnet run
# Se abrirá el navegador con la URL de la app
```

## Estructura rápida
- `Program.cs`: arranque de ASP.NET (abre navegador en inicio).
- `Controllers/`: controladores MVC (`InventarioController`, `PrediccionController`).
- `Models/`: entidades y ViewModels.
- `Views/`: Razor views (Inventario, Predicción, etc.).
- `predictor.py`: script Python que carga el modelo y devuelve JSON + genera gráfico.
- `Samples/`: CSVs de ejemplo para pruebas.
- `wwwroot/`: estáticos (imágenes, CSS opcional).

## Compartir el proyecto (GitHub)
```powershell
# Inicializar repo
git init

# Agregar todo (se excluye lo innecesario por .gitignore)
git add .

git commit -m "Tesis: MVP Inventario + Predicciones"

# Crear repo en GitHub (web) y pegar la URL remota
# Ejemplo:
git remote add origin https://github.com/TU_USUARIO/AvicolaRenzoBackend.git

git branch -M main

git push -u origin main
```

Para clonar en otra máquina:
```powershell
git clone https://github.com/TU_USUARIO/AvicolaRenzoBackend.git
cd AvicolaRenzoBackend
# Instalar deps Python si corresponde
python -m pip install -U pandas joblib matplotlib scikit-learn
# Ejecutar
dotnet run
```

## Configuración
- Base de datos: SQLite `AvicolaRenzo.db` (se crea automáticamente).
- URL de escucha: `ASPNETCORE_URLS` (opcional). Por defecto `http://localhost:5000`.
- `appsettings.json`: cadena de conexión `DefaultConnection` (SQLite).

## CSV de inventario esperado
Encabezados: `Fecha, InventarioPromedio, PrecioKg, VentasKg, PerdidasKg, Observacion`
- Formatos de fecha: `yyyy-MM-dd`, `dd/MM/yyyy`, etc.
- Delimitador: coma o punto y coma.

## Soporte
Si algo falla al ejecutar el predictor, verifica Python en PATH y dependencias instaladas. Revisa también que `modelo_ventas_simple.pkl` esté en la raíz del proyecto.
