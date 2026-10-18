using Microsoft.EntityFrameworkCore;
using TallerMecanicoSantoDomingo.Data;
using TallerMecanicoSantoDomingo.Models;

namespace TallerMecanicoSantoDomingo.Services;

// DTOs de reportes
public record FilaServicioReporte(int IdServicio, string Placa, string Cliente, string TipoServicio,
    string Estado, DateOnly FechaInicio, DateOnly? FechaFin, decimal Total);

public record FilaConteo(string Categoria, int Cantidad, decimal? Monto);

public record FilaVehiculoReporte(string Placa, string Marca, string Modelo, string Cliente, int Servicios, decimal TotalServicios);

public record FilaTrabajadorReporte(string Trabajador, string Area, int AsignacionesActivas, int AsignacionesRetiradas, int TotalAsignaciones);

public record FilaProductoReporte(string Producto, decimal CantidadTotal, int VecesUtilizado, decimal? MontoEstimado);

public record FilaFacturaReporte(int IdFactura, string NumeroFactura, DateTime Fecha, string Cliente,
    decimal Subtotal, decimal IVA, decimal Total, string Estado);

public record FilaMontoMensual(string Periodo, int Facturas, decimal Subtotal, decimal IVA, decimal Total);

public record ResultadoReporte(string Titulo, List<string> Encabezados, List<List<string>> Filas, string Nota);

/// <summary>
/// Servicio de reportes con consultas reales a SQL Server (sin datos ficticios).
/// </summary>
public interface IReporteService
{
    Task<List<FilaServicioReporte>> ServiciosPorPeriodo(DateOnly desde, DateOnly hasta, string? tipo, string? estado, int? idCliente, int? idTrabajador);
    Task<List<FilaConteo>> ServiciosPorTipo(DateOnly desde, DateOnly hasta);
    Task<List<FilaConteo>> ServiciosPorEstado(DateOnly desde, DateOnly hasta);
    Task<List<FilaVehiculoReporte>> ServiciosPorVehiculo(DateOnly desde, DateOnly hasta);
    Task<List<FilaServicioReporte>> HistorialPorCliente(int idCliente);
    Task<List<FilaTrabajadorReporte>> TrabajosPorTrabajador();
    Task<List<FilaTrabajadorReporte>> CargaActual();
    Task<List<FilaProductoReporte>> ProductosMasUtilizados(DateOnly? desde, DateOnly? hasta);
    Task<List<FilaFacturaReporte>> FacturasPorPeriodo(DateOnly desde, DateOnly hasta);
    Task<List<FilaMontoMensual>> TotalFacturadoPorPeriodo(DateOnly desde, DateOnly hasta);
}

public class ReporteService : IReporteService
{
    private readonly TallerDbContext _db;

    public ReporteService(TallerDbContext db) => _db = db;

    public async Task<List<FilaServicioReporte>> ServiciosPorPeriodo(DateOnly desde, DateOnly hasta, string? tipo, string? estado, int? idCliente, int? idTrabajador)
    {
        var query = _db.Servicios.AsNoTracking()
            .Include(s => s.Vehiculo).ThenInclude(v => v!.Cliente)
            .Where(s => s.FechaInicio >= desde && s.FechaInicio <= hasta);

        if (!string.IsNullOrWhiteSpace(tipo) && (new[] { "Mantenimiento", "Pintura" }).Contains(tipo))
            query = query.Where(s => s.TipoServicio == tipo);
        if (!string.IsNullOrWhiteSpace(estado) && (new[] { "Abierto", "Finalizado", "Cancelado" }).Contains(estado))
            query = query.Where(s => s.Estado == estado);
        if (idCliente.HasValue)
            query = query.Where(s => s.Vehiculo!.IdCliente == idCliente.Value);
        if (idTrabajador.HasValue)
            query = query.Where(s => s.DetalleServicios.Any(d => d.IdTrabajador == idTrabajador.Value));

        var servicios = await query.OrderByDescending(s => s.FechaInicio).ToListAsync();
        return servicios.Select(MapearServicio).ToList();
    }

    public async Task<List<FilaConteo>> ServiciosPorTipo(DateOnly desde, DateOnly hasta)
    {
        // La agrupación se materializa en un tipo anónimo y el orden se aplica en
        // memoria: ordenar por una propiedad de un DTO construido en el propio
        // Select no es traducible a SQL.
        var datos = await _db.Servicios.AsNoTracking()
            .Where(s => s.FechaInicio >= desde && s.FechaInicio <= hasta)
            .GroupBy(s => s.TipoServicio)
            .Select(g => new { Categoria = g.Key, Cantidad = g.Count(), Monto = (decimal?)g.Sum(s => s.Total) })
            .ToListAsync();

        return datos
            .OrderByDescending(d => d.Cantidad)
            .Select(d => new FilaConteo(d.Categoria, d.Cantidad, d.Monto))
            .ToList();
    }

    public async Task<List<FilaConteo>> ServiciosPorEstado(DateOnly desde, DateOnly hasta)
    {
        var datos = await _db.Servicios.AsNoTracking()
            .Where(s => s.FechaInicio >= desde && s.FechaInicio <= hasta)
            .GroupBy(s => s.Estado)
            .Select(g => new { Categoria = g.Key, Cantidad = g.Count(), Monto = (decimal?)g.Sum(s => s.Total) })
            .ToListAsync();

        return datos
            .OrderByDescending(d => d.Cantidad)
            .Select(d => new FilaConteo(d.Categoria, d.Cantidad, d.Monto))
            .ToList();
    }

    public async Task<List<FilaVehiculoReporte>> ServiciosPorVehiculo(DateOnly desde, DateOnly hasta)
    {
        var servicios = await _db.Servicios.AsNoTracking()
            .Include(s => s.Vehiculo).ThenInclude(v => v!.Cliente)
            .Where(s => s.FechaInicio >= desde && s.FechaInicio <= hasta)
            .ToListAsync();

        return servicios
            .GroupBy(s => new { s.Vehiculo!.Placa, s.Vehiculo.Marca, s.Vehiculo.Modelo, Cliente = s.Vehiculo.Cliente!.NombreCompleto })
            .Select(g => new FilaVehiculoReporte(g.Key.Placa, g.Key.Marca, g.Key.Modelo, g.Key.Cliente, g.Count(), g.Sum(s => s.Total)))
            .OrderByDescending(f => f.Servicios)
            .ToList();
    }

    public async Task<List<FilaServicioReporte>> HistorialPorCliente(int idCliente)
    {
        var servicios = await _db.Servicios.AsNoTracking()
            .Include(s => s.Vehiculo).ThenInclude(v => v!.Cliente)
            .Where(s => s.Vehiculo!.IdCliente == idCliente)
            .OrderByDescending(s => s.FechaInicio)
            .ToListAsync();

        return servicios.Select(MapearServicio).ToList();
    }

    public async Task<List<FilaTrabajadorReporte>> TrabajosPorTrabajador()
    {
        var trabajadores = await _db.Trabajadores.AsNoTracking()
            .Include(t => t.Area)
            .Include(t => t.DetalleServicios)
            .ToListAsync();

        return trabajadores.Select(t => new FilaTrabajadorReporte(
            t.NombreCompleto,
            t.Area?.Nombre ?? "",
            t.DetalleServicios.Count(d => d.Estado == "Activo"),
            t.DetalleServicios.Count(d => d.Estado == "Retirado"),
            t.DetalleServicios.Count))
            .OrderByDescending(f => f.TotalAsignaciones)
            .ToList();
    }

    public async Task<List<FilaTrabajadorReporte>> CargaActual()
    {
        var trabajadores = await _db.Trabajadores.AsNoTracking()
            .Include(t => t.Area)
            .Where(t => t.Activo)
            .ToListAsync();

        var activos = await _db.DetalleServicio.AsNoTracking()
            .Where(d => d.Estado == "Activo" && d.Servicio!.Estado == "Abierto")
            .GroupBy(d => d.IdTrabajador)
            .Select(g => new { g.Key, Total = g.Count() })
            .ToListAsync();

        return trabajadores.Select(t => new FilaTrabajadorReporte(
            t.NombreCompleto,
            t.Area?.Nombre ?? "",
            activos.FirstOrDefault(a => a.Key == t.IdTrabajador)?.Total ?? 0,
            0, 0))
            .OrderBy(f => f.Trabajador)
            .ToList();
    }

    public async Task<List<FilaProductoReporte>> ProductosMasUtilizados(DateOnly? desde, DateOnly? hasta)
    {
        var query = _db.ProductosUtilizados.AsNoTracking()
            .Include(pu => pu.Producto)
            .Include(pu => pu.Servicio)
            .AsEnumerable();

        if (desde.HasValue)
            query = query.Where(pu => pu.Servicio != null && pu.Servicio.FechaInicio >= desde.Value);
        if (hasta.HasValue)
            query = query.Where(pu => pu.Servicio != null && pu.Servicio.FechaInicio <= hasta.Value);

        return query
            .GroupBy(pu => pu.Producto!.Nombre)
            .Select(g => new FilaProductoReporte(
                g.Key,
                g.Sum(pu => pu.Cantidad),
                g.Count(),
                g.Sum(pu => pu.Cantidad * pu.Producto!.PrecioUnitario)))
            .OrderByDescending(f => f.CantidadTotal)
            .ToList();
    }

    public async Task<List<FilaFacturaReporte>> FacturasPorPeriodo(DateOnly desde, DateOnly hasta)
    {
        var inicio = desde.ToDateTime(TimeOnly.MinValue);
        var fin = hasta.AddDays(1).ToDateTime(TimeOnly.MinValue);

        var facturas = await _db.Facturas.AsNoTracking()
            .Include(f => f.Cliente)
            .Where(f => f.FechaFactura >= inicio && f.FechaFactura < fin)
            .OrderByDescending(f => f.FechaFactura)
            .ToListAsync();

        return facturas.Select(f => new FilaFacturaReporte(
            f.IdFactura, f.NumeroFactura, f.FechaFactura, f.Cliente?.NombreCompleto ?? "",
            f.Subtotal, f.IVA, f.Total, f.Estado)).ToList();
    }

    public async Task<List<FilaMontoMensual>> TotalFacturadoPorPeriodo(DateOnly desde, DateOnly hasta)
    {
        var inicio = desde.ToDateTime(TimeOnly.MinValue);
        var fin = hasta.AddDays(1).ToDateTime(TimeOnly.MinValue);

        var facturas = await _db.Facturas.AsNoTracking()
            .Where(f => f.FechaFactura >= inicio && f.FechaFactura < fin && f.Estado == "Emitida")
            .ToListAsync();

        return facturas
            .GroupBy(f => f.FechaFactura.ToString("yyyy-MM"))
            .Select(g => new FilaMontoMensual(
                g.Key,
                g.Count(),
                g.Sum(f => f.Subtotal),
                g.Sum(f => f.IVA),
                g.Sum(f => f.Total)))
            .OrderBy(g => g.Periodo)
            .ToList();
    }

    private static FilaServicioReporte MapearServicio(Servicio s) => new(
        s.IdServicio,
        s.Vehiculo?.Placa ?? "",
        s.Vehiculo?.Cliente?.NombreCompleto ?? "",
        s.TipoServicio,
        s.Estado,
        s.FechaInicio,
        s.FechaFin,
        s.Total);
}
