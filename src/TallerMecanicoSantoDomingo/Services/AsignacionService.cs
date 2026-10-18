using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TallerMecanicoSantoDomingo.Data;
using TallerMecanicoSantoDomingo.Models;

namespace TallerMecanicoSantoDomingo.Services;

/// <summary>Fila del listado de asignaciones de un servicio o de un trabajador.</summary>
public record ItemAsignacion(int IdDetalleServicio, int IdServicio, string DescripcionServicio, string TipoServicio,
    string EstadoServicio, int IdTrabajador, string NombreTrabajador, string Area, DateTime FechaAsignacion,
    string EstadoAsignacion, string? Observaciones);

/// <summary>
/// Servicio de negocio para la asignación de trabajadores (MER: ASIGNADO_A).
/// Reglas aplicadas: 5 (compatibilidad de área), 6 (no duplicar la pareja
/// servicio-trabajador) y 7 (máximo dos servicios activos por trabajador,
/// con transacción y bloqueo UPDLOCK/HOLDLOCK para resistir concurrencia).
/// </summary>
public interface IAsignacionService
{
    Task<Resultado> Asignar(int idServicio, int idTrabajador, string? observaciones);
    Task<Resultado> Desasignar(int idDetalleServicio);
    Task<List<ItemAsignacion>> AsignacionesDelServicio(int idServicio);
    Task<List<ItemAsignacion>> AsignacionesDelTrabajador(int idTrabajador);
    Task<List<TrabajadorCarga>> TrabajadoresCompatibles(int idServicio, bool soloDisponibles);
}

public class AsignacionService : IAsignacionService
{
    private readonly TallerDbContext _db;

    public AsignacionService(TallerDbContext db) => _db = db;

    /// <summary>
    /// Asigna un trabajador a un servicio dentro de una transacción.
    /// La cuenta de servicios activos se realiza con bloqueo UPDLOCK/HOLDLOCK
    /// sobre SQL Server para que dos solicitudes simultáneas no superen el
    /// límite de dos servicios activos (Regla 7).
    /// </summary>
    public async Task<Resultado> Asignar(int idServicio, int idTrabajador, string? observaciones)
    {
        // Validaciones básicas fuera de la transacción (lecturas).
        var servicio = await _db.Servicios.AsNoTracking()
            .FirstOrDefaultAsync(s => s.IdServicio == idServicio);
        if (servicio is null)
            return Resultado.Fallo("El servicio indicado no existe.");
        if (servicio.Estado != "Abierto")
            return Resultado.Fallo("Solo se pueden asignar trabajadores a servicios abiertos.");

        var trabajador = await _db.Trabajadores.AsNoTracking()
            .Include(t => t.Area)
            .FirstOrDefaultAsync(t => t.IdTrabajador == idTrabajador);
        if (trabajador is null)
            return Resultado.Fallo("El trabajador indicado no existe.");
        if (!trabajador.Activo)
            return Resultado.Fallo("El trabajador está desactivado y no puede recibir trabajos.");

        // Regla 5: el área del trabajador debe ser compatible con el tipo de servicio.
        var area = trabajador.Area?.Nombre ?? "";
        var compatible = (area == "Mantenimiento mecánico" && servicio.TipoServicio == "Mantenimiento")
            || (area == "Pintura" && servicio.TipoServicio == "Pintura");
        if (!compatible)
            return Resultado.Fallo($"El trabajador pertenece al área '{area}' y no puede recibir servicios de tipo '{servicio.TipoServicio}' (Regla 5).");

        var yaAsignado = await _db.DetalleServicio
            .AnyAsync(d => d.IdServicio == idServicio && d.IdTrabajador == idTrabajador);
        if (yaAsignado)
            return Resultado.Fallo("Este trabajador ya está asignado a este servicio (la pareja servicio-trabajador no se repite, Regla 6).");

        await using var transaccion = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        try
        {
            // Regla 7: bloquear primero la fila del trabajador para serializar
            // asignaciones concurrentes del mismo trabajador y luego contar sus
            // servicios activos con bloqueo de rangos.
            int activos;
            if (_db.Database.IsSqlServer())
            {
                await _db.Database.ExecuteSqlRawAsync(
                    "SELECT TOP (1) 1 FROM Trabajadores WITH (UPDLOCK, HOLDLOCK) WHERE IdTrabajador = {0}",
                    idTrabajador);

                activos = await _db.Database.SqlQueryRaw<int>(
                    "SELECT COUNT(*) AS Value FROM DetalleServicio WITH (UPDLOCK, HOLDLOCK) " +
                    "WHERE IdTrabajador = {0} AND Estado = {1}",
                    idTrabajador, "Activo").SingleAsync();
            }
            else
            {
                // Proveedores sin SQL Server (por ejemplo, el proveedor en memoria
                // usado por las pruebas unitarias): no hay bloqueo disponible.
                activos = await _db.DetalleServicio.CountAsync(d =>
                    d.IdTrabajador == idTrabajador && d.Estado == "Activo");
            }

            if (activos >= 2)
            {
                await transaccion.RollbackAsync();
                return Resultado.Fallo($"El trabajador {trabajador.NombreCompleto} ya tiene {activos} servicios activos; el máximo permitido es 2 (Regla 7).");
            }

            _db.DetalleServicio.Add(new DetalleServicio
            {
                IdServicio = idServicio,
                IdTrabajador = idTrabajador,
                FechaAsignacion = DateTime.UtcNow,
                Estado = "Activo",
                Observaciones = string.IsNullOrWhiteSpace(observaciones) ? null : observaciones.Trim()
            });

            await _db.SaveChangesAsync(); // La pareja única también está protegida por índice UNIQUE.
            await transaccion.CommitAsync();
        }
        catch (DbUpdateException)
        {
            await transaccion.RollbackAsync();
            return Resultado.Fallo("No se pudo asignar el trabajador: la asignación ya existe o fue registrada simultáneamente por otro usuario.");
        }
        catch
        {
            await transaccion.RollbackAsync();
            throw;
        }

        return Resultado.Ok($"Trabajador {trabajador.NombreCompleto} asignado correctamente.");
    }

    /// <summary>
    /// Retira la asignación de un trabajador de un servicio abierto.
    /// El registro se conserva con estado "Retirado" (Regla 14: historial).
    /// </summary>
    public async Task<Resultado> Desasignar(int idDetalleServicio)
    {
        var asignacion = await _db.DetalleServicio
            .Include(d => d.Servicio)
            .Include(d => d.Trabajador)
            .FirstOrDefaultAsync(d => d.IdDetalleServicio == idDetalleServicio);
        if (asignacion is null)
            return Resultado.Fallo("La asignación indicada no existe.");
        if (asignacion.Estado == "Retirado")
            return Resultado.Fallo("La asignación ya fue retirada.");
        if (asignacion.Servicio?.Estado != "Abierto")
            return Resultado.Fallo("Solo se pueden retirar asignaciones de servicios abiertos.");

        asignacion.Estado = "Retirado";
        await _db.SaveChangesAsync();
        return Resultado.Ok($"Se retiró a {asignacion.Trabajador?.NombreCompleto} del servicio.");
    }

    public async Task<List<ItemAsignacion>> AsignacionesDelServicio(int idServicio)
    {
        var lista = await _db.DetalleServicio.AsNoTracking()
            .Where(d => d.IdServicio == idServicio)
            .Include(d => d.Servicio)
            .Include(d => d.Trabajador).ThenInclude(t => t!.Area)
            .OrderBy(d => d.Estado).ThenBy(d => d.FechaAsignacion)
            .ToListAsync();

        return lista.Select(Mapear).ToList();
    }

    public async Task<List<ItemAsignacion>> AsignacionesDelTrabajador(int idTrabajador)
    {
        var lista = await _db.DetalleServicio.AsNoTracking()
            .Where(d => d.IdTrabajador == idTrabajador)
            .Include(d => d.Servicio).ThenInclude(s => s!.Vehiculo)
            .Include(d => d.Trabajador).ThenInclude(t => t!.Area)
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

        var trabajadores = await _db.Trabajadores.AsNoTracking()
            .Include(t => t.Area)
            .Where(t => t.Activo && t.Area!.Nombre == nombreArea)
            .ToListAsync();

        var ids = trabajadores.Select(t => t.IdTrabajador).ToList();

        var asignadosAlServicio = await _db.DetalleServicio.AsNoTracking()
            .Where(d => d.IdServicio == idServicio && d.Estado == "Activo")
            .Select(d => d.IdTrabajador)
            .ToListAsync();

        var conteos = await _db.DetalleServicio.AsNoTracking()
            .Where(d => d.Estado == "Activo" &&
                        ids.Contains(d.IdTrabajador) &&
                        d.Servicio!.Estado == "Abierto")
            .GroupBy(d => d.IdTrabajador)
            .Select(g => new { Id = g.Key, Total = g.Count() })
            .ToListAsync();

        var resultado = trabajadores.Select(t =>
        {
            var activos = conteos.FirstOrDefault(c => c.Id == t.IdTrabajador)?.Total ?? 0;
            return new TrabajadorCarga(t.IdTrabajador, t.NombreCompleto, t.Area?.Nombre ?? "", activos, activos < 2);
        })
        .Where(c => !asignadosAlServicio.Contains(c.IdTrabajador))
        .OrderBy(c => c.ServiciosActivos).ThenBy(c => c.NombreCompleto);

        return soloDisponibles ? resultado.Where(c => c.Disponible).ToList() : resultado.ToList();
    }

    private static ItemAsignacion Mapear(DetalleServicio d) => new(
        d.IdDetalleServicio,
        d.IdServicio,
        d.Servicio?.Descripcion ?? "",
        d.Servicio?.TipoServicio ?? "",
        d.Servicio?.Estado ?? "",
        d.IdTrabajador,
        d.Trabajador?.NombreCompleto ?? "",
        d.Trabajador?.Area?.Nombre ?? "",
        d.FechaAsignacion,
        d.Estado,
        d.Observaciones);
}
