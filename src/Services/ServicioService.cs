using Microsoft.EntityFrameworkCore;
using TallerMecanicoSantoDomingo.Data;
using TallerMecanicoSantoDomingo.Models;

namespace TallerMecanicoSantoDomingo.Services;

/// <summary>Trabajador con su carga actual de servicios activos.</summary>
public record TrabajadorCarga(int IdTrabajador, string NombreCompleto, string Area, int ServiciosActivos, bool Disponible);

/// <summary>
/// Servicio de negocio para la entidad SERVICIO (historial de servicios del MER).
/// Reglas aplicadas: 3 (servicios independientes por vehículo), 4 (tipo y estado),
/// 8 (validación de fechas) y 10 (importes no negativos).
/// </summary>
public class ServicioService
{
    private readonly TallerDbContext _db;

    public ServicioService(TallerDbContext db) => _db = db;

    public async Task<Resultado<Servicio>> Crear(Servicio servicio)
    {
        if (!(new[] { "Mantenimiento", "Pintura" }).Contains(servicio.TipoServicio))
            return Resultado<Servicio>.Fallo("El tipo de servicio debe ser Mantenimiento o Pintura.");

        if (string.IsNullOrWhiteSpace(servicio.Descripcion))
            return Resultado<Servicio>.Fallo("La descripción del trabajo es obligatoria.");

        if (servicio.Total < 0)
            return Resultado<Servicio>.Fallo("El monto del servicio no puede ser negativo.");

        if (servicio.FechaInicio == default)
            return Resultado<Servicio>.Fallo("La fecha de inicio es obligatoria.");

        if (servicio.FechaFin.HasValue && servicio.FechaFin.Value < servicio.FechaInicio)
            return Resultado<Servicio>.Fallo("La fecha de finalización no puede ser anterior a la fecha de inicio.");

        // Regla 3: el servicio debe corresponder a un vehículo existente.
        var vehiculo = await _db.Vehiculos.FirstOrDefaultAsync(v => v.IdVehiculo == servicio.IdVehiculo);
        if (vehiculo is null)
            return Resultado<Servicio>.Fallo("El vehículo seleccionado no existe.");
        if (!vehiculo.Activo)
            return Resultado<Servicio>.Fallo("El vehículo seleccionado está desactivado.");

        servicio.Estado = "Abierto";
        servicio.FechaRegistro = DateTime.UtcNow;
        _db.Servicios.Add(servicio);
        await _db.SaveChangesAsync();
        return Resultado<Servicio>.Ok(servicio, "Servicio registrado correctamente.");
    }

    /// <summary>
    /// Solo se pueden editar servicios abiertos que no hayan sido facturados
    /// (Regla 14: integridad histórica de las facturas).
    /// </summary>
    public async Task<Resultado> Editar(Servicio servicio)
    {
        var existente = await _db.Servicios
            .Include(s => s.DetalleFacturas).ThenInclude(df => df.Factura)
            .FirstOrDefaultAsync(s => s.IdServicio == servicio.IdServicio);
        if (existente is null)
            return Resultado.Fallo("El servicio indicado no existe.");

        if (existente.Estado != "Abierto")
            return Resultado.Fallo("Solo se pueden editar servicios abiertos.");

        if (existente.DetalleFacturas.Any(df => df.Factura != null && df.Factura.Estado == "Emitida"))
            return Resultado.Fallo("El servicio ya forma parte de una factura emitida y no puede modificarse.");

        if (string.IsNullOrWhiteSpace(servicio.Descripcion))
            return Resultado.Fallo("La descripción del trabajo es obligatoria.");
        if (servicio.Total < 0)
            return Resultado.Fallo("El monto del servicio no puede ser negativo.");
        if (servicio.FechaFin.HasValue && servicio.FechaFin.Value < servicio.FechaInicio)
            return Resultado.Fallo("La fecha de finalización no puede ser anterior a la fecha de inicio.");

        // Un servicio no cambia de vehículo ni de tipo cuando ya tiene asignaciones.
        if (existente.IdVehiculo != servicio.IdVehiculo || existente.TipoServicio != servicio.TipoServicio)
        {
            if (await _db.DetalleServicio.AnyAsync(d => d.IdServicio == existente.IdServicio))
                return Resultado.Fallo("No se puede cambiar el vehículo ni el tipo del servicio porque ya tiene trabajadores asignados.");
        }

        existente.IdVehiculo = servicio.IdVehiculo;
        existente.TipoServicio = servicio.TipoServicio;
        existente.Descripcion = servicio.Descripcion.Trim();
        existente.DescripcionFalla = servicio.DescripcionFalla?.Trim();
        existente.TipoPintura = servicio.TipoPintura?.Trim();
        existente.Color = servicio.Color?.Trim();
        existente.FechaInicio = servicio.FechaInicio;
        existente.Total = servicio.Total;
        await _db.SaveChangesAsync();
        return Resultado.Ok("Servicio actualizado correctamente.");
    }

    /// <summary>
    /// Regla 8: al finalizar un servicio debe registrarse su fecha de finalización,
    /// la cual no puede ser anterior a la fecha de inicio.
    /// </summary>
    public async Task<Resultado> Finalizar(int idServicio, DateOnly fechaFin)
    {
        var servicio = await _db.Servicios.FindAsync(idServicio);
        if (servicio is null)
            return Resultado.Fallo("El servicio indicado no existe.");
        if (servicio.Estado != "Abierto")
            return Resultado.Fallo("Solo se pueden finalizar servicios abiertos.");
        if (fechaFin < servicio.FechaInicio)
            return Resultado.Fallo("La fecha de finalización no puede ser anterior a la fecha de inicio.");

        servicio.Estado = "Finalizado";
        servicio.FechaFin = fechaFin;

        // La finalización del servicio y el cierre de sus asignaciones son una sola operación transaccional.
        var asignaciones = await _db.DetalleServicio
            .Where(d => d.IdServicio == idServicio && d.Estado == "Activo")
            .ToListAsync();
        foreach (var asignacion in asignaciones)
            asignacion.Estado = "Retirado";

        await _db.SaveChangesAsync();
        return Resultado.Ok("Servicio finalizado correctamente.");
    }

    /// <summary>Regla 8: al cancelar un servicio este deja de contar como activo.</summary>
    public async Task<Resultado> Cancelar(int idServicio, string? motivo)
    {
        var servicio = await _db.Servicios
            .Include(s => s.DetalleFacturas).ThenInclude(df => df.Factura)
            .FirstOrDefaultAsync(s => s.IdServicio == idServicio);
        if (servicio is null)
            return Resultado.Fallo("El servicio indicado no existe.");
        if (servicio.Estado != "Abierto")
            return Resultado.Fallo("Solo se pueden cancelar servicios abiertos.");
        if (servicio.DetalleFacturas.Any(df => df.Factura != null && df.Factura.Estado == "Emitida"))
            return Resultado.Fallo("El servicio ya forma parte de una factura emitida y no puede cancelarse.");

        servicio.Estado = "Cancelado";

        var asignaciones = await _db.DetalleServicio
            .Where(d => d.IdServicio == idServicio && d.Estado == "Activo")
            .ToListAsync();
        foreach (var asignacion in asignaciones)
        {
            asignacion.Estado = "Retirado";
            if (!string.IsNullOrWhiteSpace(motivo))
                asignacion.Observaciones = $"Cancelación: {motivo.Trim()}";
        }

        await _db.SaveChangesAsync();
        return Resultado.Ok("Servicio cancelado. Los trabajadores quedan disponibles para otros trabajos.");
    }

    public async Task<ResultadoPaginado<Servicio>> Buscar(string? termino, string? tipo, string? estado, int? idVehiculo, int pagina, int tamPagina)
    {
        pagina = Math.Max(1, pagina);
        tamPagina = Math.Clamp(tamPagina, 5, 200);

        IQueryable<Servicio> query = _db.Servicios.AsNoTracking()
            .Include(s => s.Vehiculo).ThenInclude(v => v!.Cliente)
            .Include(s => s.DetalleServicios).ThenInclude(d => d.Trabajador);

        if (!string.IsNullOrWhiteSpace(tipo) && (new[] { "Mantenimiento", "Pintura" }).Contains(tipo))
            query = query.Where(s => s.TipoServicio == tipo);
        if (!string.IsNullOrWhiteSpace(estado) && (new[] { "Abierto", "Finalizado", "Cancelado" }).Contains(estado))
            query = query.Where(s => s.Estado == estado);
        if (idVehiculo.HasValue)
            query = query.Where(s => s.IdVehiculo == idVehiculo.Value);
        if (!string.IsNullOrWhiteSpace(termino))
        {
            var t = termino.Trim();
            query = query.Where(s =>
                s.Descripcion.Contains(t) ||
                (s.Vehiculo != null && (s.Vehiculo.Placa.Contains(t) ||
                    (s.Vehiculo.Cliente != null && (s.Vehiculo.Cliente.Nombre.Contains(t) || s.Vehiculo.Cliente.Apellido.Contains(t))))));
        }

        var total = await query.CountAsync();
        var elementos = await query
            .OrderByDescending(s => s.FechaRegistro)
            .Skip((pagina - 1) * tamPagina).Take(tamPagina)
            .ToListAsync();

        return new ResultadoPaginado<Servicio>(elementos, total, pagina, tamPagina);
    }

    public Task<Servicio?> ObtenerPorId(int idServicio) =>
        _db.Servicios.AsNoTracking()
            .Include(s => s.Vehiculo).ThenInclude(v => v!.Cliente)
            .Include(s => s.DetalleServicios).ThenInclude(d => d.Trabajador)
            .Include(s => s.ProductosUtilizados).ThenInclude(pu => pu.Producto)
            .Include(s => s.DetalleFacturas).ThenInclude(df => df.Factura)
            .FirstOrDefaultAsync(s => s.IdServicio == idServicio);

    /// <summary>Calcula con datos reales la carga de una lista de trabajadores.</summary>
    public async Task<List<TrabajadorCarga>> CargasDeTrabajadores(List<int> idsTrabajadores)
    {
        var trabajadores = await _db.Trabajadores.AsNoTracking()
            .Where(t => idsTrabajadores.Contains(t.IdTrabajador))
            .Select(t => new
            {
                t.IdTrabajador,
                Nombre = t.Nombre + " " + t.Apellido,
                t.Area,
                Activos = t.DetalleServicios.Count(d => d.Estado == "Activo" && d.Servicio!.Estado == "Abierto")
            }).ToListAsync();

        return trabajadores.Select(t => new TrabajadorCarga(
            t.IdTrabajador, t.Nombre, t.Area, t.Activos, t.Activos < 2)).ToList();
    }
}
