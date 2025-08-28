using Microsoft.EntityFrameworkCore;
using AvicolaRenzoPredictor.Data;
using Microsoft.Data.Sqlite;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddControllersWithViews();

var app = builder.Build();

// Asegurar base de datos y tablas mínimas (incluida Predicciones)
using (var scope = app.Services.CreateScope())
{
    var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    ctx.Database.EnsureCreated();

    var conn = ctx.Database.GetDbConnection();
    conn.Open();
    using var check = conn.CreateCommand();
    check.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='Predicciones';";
    var exists = check.ExecuteScalar();
    if (exists == null)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"CREATE TABLE Predicciones (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Fecha TEXT NOT NULL,
            InventarioPromedio REAL NOT NULL,
            PrecioKg REAL NOT NULL,
            VentasPred REAL NOT NULL,
            AbastecerKg REAL NOT NULL,
            Alerta TEXT NOT NULL,
            ModeloVersion TEXT NULL,
            CreatedAt TEXT NOT NULL
        );";
        cmd.ExecuteNonQuery();
    }
    conn.Close();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Inventario}/{action=Index}/{id?}");

// Abrir navegador automáticamente al iniciar (usa la primera URL disponible)
app.Lifetime.ApplicationStarted.Register(() =>
{
    try
    {
        var url = app.Urls.FirstOrDefault() ?? Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "http://localhost:5000";
        if (!url.EndsWith('/')) url += "/";

        // Intenta abrir con el navegador predeterminado
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch
        {
            // Fallback para Windows
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
            }
        }
    }
    catch { }
});

app.Run();
