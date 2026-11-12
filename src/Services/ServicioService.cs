using Microsoft.EntityFrameworkCore;
using TallerMecanicoSantoDomingo.Data;
using TallerMecanicoSantoDomingo.Models;

namespace TallerMecanicoSantoDomingo.Services;

public record TrabajadorCarga(int IdTrabajador, string NombreCompleto, string Area, int ServiciosActivos, bool Disponible);

public class ServicioService
{
    private readonly TallerDbContext _db;
    public ServicioService(TallerDbContext db) => _db = db;

    public Task<Resultado<Servicio>> Crear(Servicio servicio, int? idTrabajador = null) =>
        OperacionTaller.Ejecutar(_db, async () =>
        {
            Normalizar(servicio);
            if (ReglasTaller.DatosServicio(servicio) is { } error) return Resultado<Servicio>.Fallo(error);
            if (servicio.IdServicio != 0 || servicio.Estado != ReglasTaller.Abierto || servicio.FechaFin.HasValue)
                return Resultado<Servicio>.Fallo("Un servicio nuevo debe abrirse sin fecha de finalización ni identificador manual.");
            if (await ValidarVehiculo(servicio.IdVehiculo) is { } vehiculo) return Resultado<Servicio>.Fallo(vehiculo);
            if (!idTrabajador.HasValue || idTrabajador <= 0)
                return Resultado<Servicio>.Fallo("Seleccione al menos un trabajador habilitado para abrir el servicio.");
            var trabajador = await _db.Trabajadores.SingleOrDefaultAsync(t => t.IdTrabajador == idTrabajador);
            if (await ReglasTaller.PuedeAsignar(_db, trabajador, servicio.TipoServicio) is { } personal)
                return Resultado<Servicio>.Fallo(personal);

            servicio.FechaRegistro = DateTime.UtcNow;
            servicio.DetalleServicios = [new DetalleServicio { IdTrabajador = idTrabajador.Value, Estado = ReglasTaller.Asignado }];
            // Las relaciones recibidas nunca se usan para crear consumos, facturas o vehículos por asignación masiva.
            servicio.Vehiculo = null;
            servicio.ProductosUtilizados = [];
            servicio.DetalleFacturas = [];
            _db.Servicios.Add(servicio);
            await _db.SaveChangesAsync();
            return Resultado<Servicio>.Ok(servicio, "Servicio abierto con su trabajador inicial.");
        }, Resultado<Servicio>.Fallo);

    public Task<Resultado> Editar(Servicio servicio) => OperacionTaller.Ejecutar(_db, async () =>
    {
        var existente = await _db.Servicios.SingleOrDefaultAsync(s => s.IdServicio == servicio.IdServicio);
        if (existente is null) return Resultado.Fallo("El servicio indicado no existe.");
        if (await ReglasTaller.Facturado(_db, servicio.IdServicio))
            return Resultado.Fallo("Este servicio ya tiene una factura vigente; anúlela antes de corregirlo.");
        if (existente.Estado != ReglasTaller.Abierto) return Resultado.Fallo("Solo se pueden editar servicios abiertos.");
        Normalizar(servicio);
        servicio.Estado = existente.Estado;
        servicio.FechaFin = existente.FechaFin;
        if (ReglasTaller.DatosServicio(servicio) is { } error) return Resultado.Fallo(error);
        if (await ValidarVehiculo(servicio.IdVehiculo) is { } vehiculo) return Resultado.Fallo(vehiculo);
        if (existente.IdVehiculo != servicio.IdVehiculo || existente.TipoServicio != servicio.TipoServicio)
        {
            if (await _db.DetalleServicio.AnyAsync(d => d.IdServicio == existente.IdServicio) ||
                await _db.ProductosUtilizados.AnyAsync(p => p.IdServicio == existente.IdServicio) ||
                await _db.DetalleFactura.AnyAsync(d => d.IdServicio == existente.IdServicio))
                return Resultado.Fallo("No se puede cambiar el vehículo ni el tipo de un servicio con asignaciones, consumos o historial de facturación.");
        }
        if (await ValidarPersonal(existente.IdServicio, servicio.TipoServicio) is { } personal) return Resultado.Fallo(personal);
        existente.IdVehiculo = servicio.IdVehiculo;
        existente.TipoServicio = servicio.TipoServicio;
        existente.Descripcion = servicio.Descripcion;
        existente.DescripcionFalla = servicio.DescripcionFalla;
        existente.TipoPintura = servicio.TipoPintura;
        existente.Color = servicio.Color;
        existente.FechaInicio = servicio.FechaInicio;
        existente.Total = servicio.Total;
        await _db.SaveChangesAsync();
        return Resultado.Ok("Servicio actualizado correctamente.");
    }, Resultado.Fallo);

    public Task<Resultado> Finalizar(int idServicio, DateOnly fechaFin) => CambiarEstado(idServicio, ReglasTaller.Finalizado, fechaFin, null);
    public Task<Resultado> Cancelar(int idServicio, string? motivo) => CambiarEstado(idServicio, ReglasTaller.Cancelado, null, motivo);

    private Task<Resultado> CambiarEstado(int idServicio, string destino, DateOnly? fechaFin, string? motivo) =>
        OperacionTaller.Ejecutar(_db, async () =>
        {
            var servicio = await _db.Servicios.SingleOrDefaultAsync(s => s.IdServicio == idServicio);
            if (servicio is null) return Resultado.Fallo("El servicio indicado no existe.");
            if (await ReglasTaller.Facturado(_db, idServicio)) return Resultado.Fallo("Este servicio ya tiene una factura vigente.");
            if (servicio.Estado != ReglasTaller.Abierto) return Resultado.Fallo("Solo se pueden finalizar o cancelar servicios abiertos.");
            motivo = ReglasTaller.Opcional(motivo);
            if (motivo?.Length > 200) return Resultado.Fallo("El motivo no puede exceder 200 caracteres.");
            if (destino == ReglasTaller.Finalizado)
            {
                if (fechaFin is null || fechaFin == default(DateOnly)) return Resultado.Fallo("La fecha de finalización es obligatoria.");
                if (fechaFin < servicio.FechaInicio) return Resultado.Fallo("La fecha de finalización no puede ser anterior a la fecha de inicio.");
                if (ReglasTaller.DatosServicio(servicio) is { } error) return Resultado.Fallo(error);
                if (await ValidarPersonal(idServicio, servicio.TipoServicio) is { } personal) return Resultado.Fallo(personal);
            }
            var asignaciones = await _db.DetalleServicio.Where(d => d.IdServicio == idServicio).ToListAsync();
            if (asignaciones.Any(d => d.Estado is not (ReglasTaller.Asignado or ReglasTaller.Retirado)))
                return Resultado.Fallo("Hay una asignación con estado desconocido. Revísela antes de continuar.");
            if (destino == ReglasTaller.Cancelado && motivo is not null &&
                asignaciones.Where(a => a.Estado == ReglasTaller.Asignado).Any(a =>
                    ((a.Observaciones is null ? "" : a.Observaciones + " | ") + "Cancelación: " + motivo).Length > 200))
                return Resultado.Fallo("El motivo y las observaciones existentes superan los 200 caracteres disponibles. Acorte el motivo para conservar ambos completos.");
            servicio.Estado = destino;
            if (destino == ReglasTaller.Finalizado) servicio.FechaFin = fechaFin;
            foreach (var asignacion in asignaciones.Where(a => a.Estado == ReglasTaller.Asignado))
            {
                asignacion.Estado = ReglasTaller.Retirado;
                if (destino == ReglasTaller.Cancelado && motivo is not null)
                    asignacion.Observaciones = (asignacion.Observaciones is null ? "" : asignacion.Observaciones + " | ") + "Cancelación: " + motivo;
            }
            await _db.SaveChangesAsync();
            return Resultado.Ok(destino == ReglasTaller.Finalizado ? "Servicio finalizado correctamente." : "Servicio cancelado; sus asignaciones quedan cerradas.");
        }, Resultado.Fallo);

    private async Task<string?> ValidarVehiculo(int id)
    {
        var vehiculo = await _db.Vehiculos.Include(v => v.Cliente).SingleOrDefaultAsync(v => v.IdVehiculo == id);
        if (vehiculo is null) return "El vehículo seleccionado no existe.";
        if (!vehiculo.Activo || vehiculo.Cliente is null || !vehiculo.Cliente.Activo)
            return "El vehículo o su cliente están desactivados o no son válidos para recibir trabajos.";
        return null;
    }

    private async Task<string?> ValidarPersonal(int idServicio, string tipo)
    {
        var personal = await _db.DetalleServicio.Where(d => d.IdServicio == idServicio && d.Estado == ReglasTaller.Asignado)
            .Include(d => d.Trabajador).ToListAsync();
        if (personal.Count == 0) return "Un servicio abierto necesita al menos un trabajador habilitado.";
        foreach (var asignacion in personal)
        {
            var trabajador = asignacion.Trabajador;
            if (trabajador is null || !trabajador.Activo || trabajador.FechaSalida.HasValue || !ReglasTaller.Compatible(trabajador.Area, tipo))
                return "El servicio tiene un trabajador inactivo, con salida o de otra área. Resuelva la asignación primero.";
        }
        return null;
    }

    private static void Normalizar(Servicio servicio)
    {
        servicio.Descripcion = servicio.Descripcion?.Trim() ?? "";
        servicio.TipoServicio = servicio.TipoServicio?.Trim() ?? "";
        servicio.DescripcionFalla = ReglasTaller.Opcional(servicio.DescripcionFalla);
        servicio.TipoPintura = ReglasTaller.Opcional(servicio.TipoPintura);
        servicio.Color = ReglasTaller.Opcional(servicio.Color);
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
                t.Area, t.Activo, t.FechaSalida,
                Activos = t.DetalleServicios.Where(d => d.Estado == "Activo" && ReglasTaller.EstadosActivos.Contains(d.Servicio!.Estado)).Select(d => d.IdServicio).Distinct().Count()
            }).ToListAsync();

        return trabajadores.Select(t => new TrabajadorCarga(
            t.IdTrabajador, t.Nombre, t.Area, t.Activos, t.Activo && t.FechaSalida == null && t.Activos < 2)).ToList();
    }
}
