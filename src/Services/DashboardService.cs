using Microsoft.EntityFrameworkCore;
using TallerMecanicoSantoDomingo.Data;
using TallerMecanicoSantoDomingo.Models;

namespace TallerMecanicoSantoDomingo.Services;

/// <summary>Datos del panel principal, todos consultados desde SQL Server.</summary>
public record DatosDashboard(
    int TotalClientes,
    int TotalVehiculos,
    int ServiciosActivos,
    int ServiciosFinalizados,
    int TotalTrabajadores,
    int TotalProductos,
    int FacturasEmitidas,
    decimal ImporteFacturado,
    List<string> EtiquetasServiciosPorTipo,
    List<int> ValoresServiciosPorTipo,
    List<string> EtiquetasServiciosPorEstado,
    List<int> ValoresServiciosPorEstado,
    List<string> EtiquetasFacturacionMensual,
    List<decimal> ValoresFacturacionMensual,
    List<ServicioResumen> UltimosServicios,
    List<string> TrabajadoresAlLimite);

public record ServicioResumen(int IdServicio, DateOnly FechaInicio, string Placa, string Cliente,
    string TipoServicio, string Estado, decimal Total);

/// <summary>Servicio del panel principal (dashboard).</summary>
public class DashboardService
{
    private readonly TallerDbContext _db;

    public DashboardService(TallerDbContext db) => _db = db;

    public async Task<DatosDashboard> ObtenerDatos()
    {
        // Tarjetas: cifras reales; 0 cuando la base está vacía.
        var totalClientes = await _db.Clientes.CountAsync();
        var totalVehiculos = await _db.Vehiculos.CountAsync();
        var serviciosActivos = await _db.Servicios.CountAsync(s => s.Estado == "Abierto");
        var serviciosFinalizados = await _db.Servicios.CountAsync(s => s.Estado == "Finalizado");
        var totalTrabajadores = await _db.Trabajadores.CountAsync();
        var totalProductos = await _db.Productos.CountAsync();
        var facturasEmitidas = await _db.Facturas.CountAsync(f => f.Estado == "Emitida");
        var importeFacturado = await _db.Facturas
            .Where(f => f.Estado == "Emitida")
            .SumAsync(f => (decimal?)f.Total) ?? 0m;

        // Servicios por tipo y por estado (histórico completo).
        var porTipo = await _db.Servicios.AsNoTracking()
            .GroupBy(s => s.TipoServicio)
            .Select(g => new { g.Key, Total = g.Count() })
            .ToListAsync();
        var porEstado = await _db.Servicios.AsNoTracking()
            .GroupBy(s => s.Estado)
            .Select(g => new { g.Key, Total = g.Count() })
            .ToListAsync();

        // Evolución de la facturación: últimos 12 meses (solo facturas emitidas).
        var hoy = DateTime.Today;
        var inicio = new DateTime(hoy.Year, hoy.Month, 1).AddMonths(-11);
        var facturasUltimos12Meses = await _db.Facturas.AsNoTracking()
            .Where(f => f.Estado == "Emitida" && f.FechaFactura >= inicio)
            .ToListAsync();

        var etiquetasMes = new List<string>();
        var valoresMes = new List<decimal>();
        for (var i = 0; i < 12; i++)
        {
            var mes = inicio.AddMonths(i);
            var siguiente = mes.AddMonths(1);
            etiquetasMes.Add(mes.ToString("MM/yyyy"));
            valoresMes.Add(facturasUltimos12Meses
                .Where(f => f.FechaFactura >= mes && f.FechaFactura < siguiente)
                .Sum(f => f.Total));
        }

        // Últimos servicios registrados.
        var ultimos = await _db.Servicios.AsNoTracking()
            .Include(s => s.Vehiculo).ThenInclude(v => v!.Cliente)
            .OrderByDescending(s => s.FechaRegistro)
            .Take(10)
            .ToListAsync();

        var resumenes = ultimos.Select(s => new ServicioResumen(
            s.IdServicio, s.FechaInicio, s.Vehiculo?.Placa ?? "", s.Vehiculo?.Cliente?.NombreCompleto ?? "",
            s.TipoServicio, s.Estado, s.Total)).ToList();

        // Alerta: trabajadores con dos servicios activos (límite de la Regla 7).
        // Se agrupa por columnas mapeadas (Nombre/Apellido) y el texto se arma en
        // memoria, porque NombreCompleto es una propiedad calculada y no se puede
        // traducir a SQL.
        var trabajadoresAlLimite = (await _db.DetalleServicio.AsNoTracking()
                .Where(d => d.Estado == "Activo"
                            && d.Servicio!.Estado == "Abierto")
                .GroupBy(d => new { d.IdTrabajador, d.Trabajador!.Nombre, d.Trabajador.Apellido })
                .Where(g => g.Count() >= 2)
                .Select(g => new { g.Key.Nombre, g.Key.Apellido })
                .ToListAsync())
            .Select(t => $"{t.Nombre} {t.Apellido}")
            .ToList();

        return new DatosDashboard(
            totalClientes, totalVehiculos, serviciosActivos, serviciosFinalizados,
            totalTrabajadores, totalProductos, facturasEmitidas, importeFacturado,
            porTipo.Select(p => p.Key).ToList(),
            porTipo.Select(p => p.Total).ToList(),
            porEstado.Select(p => p.Key).ToList(),
            porEstado.Select(p => p.Total).ToList(),
            etiquetasMes, valoresMes,
            resumenes, trabajadoresAlLimite);
    }
}
