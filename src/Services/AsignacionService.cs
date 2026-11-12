using Microsoft.EntityFrameworkCore;
using TallerMecanicoSantoDomingo.Data;
using TallerMecanicoSantoDomingo.Models;

namespace TallerMecanicoSantoDomingo.Services;

public record ItemAsignacion(int IdServicio, string DescripcionServicio, string TipoServicio,
    string EstadoServicio, int IdTrabajador, string NombreTrabajador, string Area, DateTime FechaAsignacion,
    string EstadoAsignacion, string? Observaciones);

public class AsignacionService
{
    private readonly TallerDbContext _db;
    public AsignacionService(TallerDbContext db) => _db = db;

    public Task<Resultado> Asignar(int idServicio, int idTrabajador, string? observaciones) =>
        OperacionTaller.Ejecutar(_db, async () =>
        {
            var servicio = await _db.Servicios.SingleOrDefaultAsync(s => s.IdServicio == idServicio);
            if (servicio is null) return Resultado.Fallo("El servicio indicado no existe.");
            if (servicio.Estado != ReglasTaller.Abierto) return Resultado.Fallo("Solo se pueden asignar trabajadores a servicios abiertos.");
            if (await ReglasTaller.Facturado(_db, idServicio)) return Resultado.Fallo("Este servicio ya tiene una factura vigente.");
            observaciones = ReglasTaller.Opcional(observaciones);
            if (observaciones?.Length > 200) return Resultado.Fallo("Las observaciones no pueden exceder 200 caracteres.");
            var asignacion = await _db.DetalleServicio.SingleOrDefaultAsync(d => d.IdServicio == idServicio && d.IdTrabajador == idTrabajador);
            if (asignacion?.Estado == ReglasTaller.Asignado) return Resultado.Fallo("Este trabajador ya está asignado a este servicio.");
            if (asignacion is not null && asignacion.Estado != ReglasTaller.Retirado)
                return Resultado.Fallo("La asignación tiene un estado desconocido; revísela antes de reactivarla.");
            var trabajador = await _db.Trabajadores.SingleOrDefaultAsync(t => t.IdTrabajador == idTrabajador);
            if (await ReglasTaller.PuedeAsignar(_db, trabajador, servicio.TipoServicio) is { } error) return Resultado.Fallo(error);
            if (asignacion is null)
            {
                _db.DetalleServicio.Add(new DetalleServicio { IdServicio = idServicio, IdTrabajador = idTrabajador,
                    Estado = ReglasTaller.Asignado, Observaciones = observaciones });
            }
            else
            {
                // Reactivar la misma pareja; no se insertan duplicados ni se borran observaciones históricas.
                if (observaciones is not null && observaciones != asignacion.Observaciones)
                {
                    var combinadas = (asignacion.Observaciones is null ? "" : asignacion.Observaciones + " | ") + observaciones;
                    if (combinadas.Length > 200)
                        return Resultado.Fallo("Las observaciones nuevas y las anteriores superan 200 caracteres; acorte las nuevas para conservar ambas.");
                    asignacion.Observaciones = combinadas;
                }
                asignacion.Estado = ReglasTaller.Asignado;
            }
            await _db.SaveChangesAsync();
            return Resultado.Ok("Trabajador asignado correctamente.");
        }, Resultado.Fallo);

    public Task<Resultado> Desasignar(int idServicio, int idTrabajador) => OperacionTaller.Ejecutar(_db, async () =>
    {
        var asignacion = await _db.DetalleServicio.Include(d => d.Servicio)
            .SingleOrDefaultAsync(d => d.IdServicio == idServicio && d.IdTrabajador == idTrabajador);
        if (asignacion is null) return Resultado.Fallo("La asignación indicada no existe.");
        if (asignacion.Estado != ReglasTaller.Asignado) return Resultado.Fallo("La asignación no está activa.");
        if (asignacion.Servicio?.Estado != ReglasTaller.Abierto) return Resultado.Fallo("Solo se pueden retirar asignaciones de servicios abiertos.");
        if (await ReglasTaller.Facturado(_db, idServicio)) return Resultado.Fallo("Este servicio ya tiene una factura vigente.");
        var restantes = await _db.DetalleServicio.Where(d => d.IdServicio == idServicio && d.IdTrabajador != idTrabajador && d.Estado == ReglasTaller.Asignado)
            .Include(d => d.Trabajador).ToListAsync();
        if (!restantes.Any(d => d.Trabajador is { Activo: true, FechaSalida: null } t && ReglasTaller.Compatible(t.Area, asignacion.Servicio.TipoServicio)))
            return Resultado.Fallo("No puede retirar al último trabajador habilitado de un servicio abierto. Asigne un reemplazo o cierre el servicio primero.");
        asignacion.Estado = ReglasTaller.Retirado;
        await _db.SaveChangesAsync();
        return Resultado.Ok("Asignación retirada; su registro histórico se conserva.");
    }, Resultado.Fallo);

    public async Task<List<ItemAsignacion>> AsignacionesDelServicio(int idServicio)
    {
        var lista = await _db.DetalleServicio.AsNoTracking()
            .Where(d => d.IdServicio == idServicio)
            .Include(d => d.Servicio)
            .Include(d => d.Trabajador)
            .OrderBy(d => d.Estado).ThenBy(d => d.FechaAsignacion)
            .ToListAsync();

        return lista.Select(Mapear).ToList();
    }

    public async Task<List<ItemAsignacion>> AsignacionesDelTrabajador(int idTrabajador)
    {
        var lista = await _db.DetalleServicio.AsNoTracking()
            .Where(d => d.IdTrabajador == idTrabajador)
            .Include(d => d.Servicio).ThenInclude(s => s!.Vehiculo)
            .Include(d => d.Trabajador)
            .OrderByDescending(d => d.FechaAsignacion)
            .ToListAsync();

        return lista.Select(Mapear).ToList();
    }

    /// <summary>
    /// Trabajadores compatibles con el servicio (Regla 5) con su carga real;
    /// opcionalmente solo los disponibles (menos de dos servicios activos, Regla 7).
    /// </summary>
    public async Task<List<TrabajadorCarga>> TrabajadoresCompatibles(int idServicio, bool soloDisponibles)
    {
        var servicio = await _db.Servicios.AsNoTracking().FirstOrDefaultAsync(s => s.IdServicio == idServicio);
        if (servicio is null)
            return new List<TrabajadorCarga>();

        var nombreArea = servicio.TipoServicio == "Mantenimiento"
            ? "Mantenimiento mecánico" : "Pintura";

        // Contar desde la relación evita cargar listas y cruzarlas en memoria.
        var query = _db.Trabajadores.AsNoTracking()
            .Where(t => t.Activo && t.FechaSalida == null && t.Area == nombreArea)
            .Where(t => !t.DetalleServicios.Any(d => d.IdServicio == idServicio && d.Estado == "Activo"))
            .Select(t => new
            {
                t.IdTrabajador,
                Nombre = t.Nombre + " " + t.Apellido,
                t.Area,
                Activos = t.DetalleServicios.Where(d => d.Estado == "Activo" && ReglasTaller.EstadosActivos.Contains(d.Servicio!.Estado)).Select(d => d.IdServicio).Distinct().Count()
            });
        if (soloDisponibles)
            query = query.Where(t => t.Activos < 2);

        var trabajadores = await query.OrderBy(t => t.Activos).ThenBy(t => t.Nombre).ToListAsync();
        return trabajadores.Select(t => new TrabajadorCarga(
            t.IdTrabajador, t.Nombre, t.Area, t.Activos, t.Activos < 2)).ToList();
    }

    private static ItemAsignacion Mapear(DetalleServicio d) => new(
        d.IdServicio,
        d.Servicio?.Descripcion ?? "",
        d.Servicio?.TipoServicio ?? "",
        d.Servicio?.Estado ?? "",
        d.IdTrabajador,
        d.Trabajador?.NombreCompleto ?? "",
        d.Trabajador?.Area ?? "",
        d.FechaAsignacion,
        d.Estado,
        d.Observaciones);
}
