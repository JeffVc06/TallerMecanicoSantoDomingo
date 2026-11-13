using Microsoft.EntityFrameworkCore;
using TallerMecanicoSantoDomingo.Data;
using TallerMecanicoSantoDomingo.Models;

namespace TallerMecanicoSantoDomingo.Services;

/// <summary>Ítem del historial completo de un vehículo.</summary>
public record ItemHistorial(
    int IdServicio,
    DateOnly FechaInicio,
    DateOnly? FechaFin,
    string TipoServicio,
    string Descripcion,
    string Estado,
    decimal Total,
    List<string> Trabajadores,
    List<string> Productos,
    List<string> Facturas);

/// <summary>
/// Servicio de negocio para la entidad VEHÍCULO.
/// Reglas aplicadas: 2 (un vehículo pertenece a un único cliente registrado),
/// 3 (historial de servicios) y 14 (desactivación lógica).
/// </summary>
public class VehiculoService
{
    private readonly TallerDbContext _db;

    public VehiculoService(TallerDbContext db) => _db = db;

    public Task<Resultado> Crear(Vehiculo vehiculo) => OperacionTaller.Ejecutar(_db, async () =>
    {
        if (vehiculo.IdVehiculo != 0) return Resultado.Fallo("El identificador del vehículo se genera automáticamente.");
        var error = await Validar(vehiculo);
        if (error is not null)
            return error;

        vehiculo.FechaRegistro = DateTime.UtcNow;
        vehiculo.Activo = true;
        vehiculo.Cliente = null;
        vehiculo.Servicios = [];
        _db.Vehiculos.Add(vehiculo);
        await _db.SaveChangesAsync();
        return Resultado.Ok("Vehículo registrado correctamente.");
    }, Resultado.Fallo);

    public Task<Resultado> Editar(Vehiculo vehiculo) => OperacionTaller.Ejecutar(_db, async () =>
    {
        var existente = await _db.Vehiculos.FindAsync(vehiculo.IdVehiculo);
        if (existente is null)
            return Resultado.Fallo("El vehículo indicado no existe.");

        var error = await Validar(vehiculo, existente.IdCliente);
        if (error is not null)
            return error;

        if (existente.IdCliente != vehiculo.IdCliente && await _db.Servicios.AnyAsync(x => x.IdVehiculo == existente.IdVehiculo))
            return Resultado.Fallo("No se puede cambiar el propietario de un vehículo con historial de servicios. El sistema no dispone de historial de transferencias de propiedad.");
        existente.IdCliente = vehiculo.IdCliente;
        existente.Placa = vehiculo.Placa;
        existente.Marca = vehiculo.Marca;
        existente.Modelo = vehiculo.Modelo;
        existente.Anio = vehiculo.Anio;
        existente.Kilometraje = vehiculo.Kilometraje;
        existente.Color = vehiculo.Color;
        existente.NumChasis = vehiculo.NumChasis;
        existente.Tipo = vehiculo.Tipo;
        await _db.SaveChangesAsync();
        return Resultado.Ok("Vehículo actualizado correctamente.");
    }, Resultado.Fallo);

    private async Task<Resultado?> Validar(Vehiculo vehiculo, int? propietarioActual = null)
    {
        vehiculo.Placa = (vehiculo.Placa?.Trim() ?? "").ToUpperInvariant();
        vehiculo.Marca = vehiculo.Marca?.Trim() ?? "";
        vehiculo.Modelo = vehiculo.Modelo?.Trim() ?? "";
        vehiculo.Color = string.IsNullOrWhiteSpace(vehiculo.Color) ? null : vehiculo.Color.Trim();
        vehiculo.NumChasis = string.IsNullOrWhiteSpace(vehiculo.NumChasis) ? null : vehiculo.NumChasis.Trim().ToUpperInvariant();
        vehiculo.Tipo = string.IsNullOrWhiteSpace(vehiculo.Tipo) ? null : vehiculo.Tipo.Trim();

        if (ReglasTaller.Campos(vehiculo) is { } campos) return Resultado.Fallo(campos);
        if (vehiculo.Placa.Length == 0)
            return Resultado.Fallo("La placa del vehículo es obligatoria.");
        if (!ReglasTaller.IdentificadorVehiculoValido(vehiculo.Placa) || !ReglasTaller.IdentificadorVehiculoValido(vehiculo.NumChasis))
            return Resultado.Fallo($"Placa o número de chasis: {ReglasTaller.ErrorIdentificadorVehiculo}");
        if (!ReglasTaller.AnioVehiculoValido(vehiculo.Anio))
            return Resultado.Fallo(ReglasTaller.ErrorAnio);
        if (vehiculo.Kilometraje < 0)
            return Resultado.Fallo("El kilometraje debe ser mayor o igual a cero.");
        var cliente = await _db.Clientes.AsNoTracking().SingleOrDefaultAsync(c => c.IdCliente == vehiculo.IdCliente);
        if (cliente is null)
            return Resultado.Fallo("El cliente seleccionado no existe.");
        if (!cliente.Activo && propietarioActual != cliente.IdCliente)
            return Resultado.Fallo("El cliente está desactivado. Reactívelo antes de registrar o transferirle un vehículo.");

        if (await ReglasTaller.IdentificadorVehiculoRepetido(_db, vehiculo.Placa, chasis: false, vehiculo.IdVehiculo))
            return Resultado.Fallo($"Ya existe un vehículo registrado con la placa {vehiculo.Placa}.");
        if (vehiculo.NumChasis is not null && await ReglasTaller.IdentificadorVehiculoRepetido(_db, vehiculo.NumChasis, chasis: true, vehiculo.IdVehiculo))
            return Resultado.Fallo($"Ya existe un vehículo registrado con el número de chasis {vehiculo.NumChasis}.");

        return null;
    }

    /// <summary>Regla 14: los vehículos con servicios no se eliminan; se desactivan.</summary>
    public Task<Resultado> CambiarEstado(int idVehiculo, bool activo) => OperacionTaller.Ejecutar(_db, async () =>
    {
        var vehiculo = await _db.Vehiculos.FindAsync(idVehiculo);
        if (vehiculo is null)
            return Resultado.Fallo("El vehículo indicado no existe.");

        if (!activo)
        {
            var tieneServiciosAbiertos = await _db.Servicios
                .AnyAsync(s => s.IdVehiculo == idVehiculo && ReglasTaller.EstadosActivos.Contains(s.Estado));
            if (tieneServiciosAbiertos)
                return Resultado.Fallo("No se puede desactivar el vehículo porque tiene servicios abiertos.");
        }

        vehiculo.Activo = activo;
        await _db.SaveChangesAsync();
        return Resultado.Ok(activo ? "Vehículo reactivado correctamente." : "Vehículo desactivado.");
    }, Resultado.Fallo);

    public async Task<ResultadoPaginado<Vehiculo>> Buscar(string? termino, int? idCliente, bool? soloActivos, int pagina, int tamPagina)
    {
        pagina = Math.Max(1, pagina);
        tamPagina = Math.Clamp(tamPagina, 5, 200);

        IQueryable<Vehiculo> query = _db.Vehiculos.AsNoTracking().Include(v => v.Cliente);
        if (soloActivos == true)
            query = query.Where(v => v.Activo);
        if (idCliente.HasValue)
            query = query.Where(v => v.IdCliente == idCliente.Value);
        if (!string.IsNullOrWhiteSpace(termino))
        {
            var t = termino.Trim();
            query = query.Where(v =>
                v.Placa.Contains(t) || v.Marca.Contains(t) || v.Modelo.Contains(t) ||
                (v.Cliente != null && (v.Cliente.Nombre.Contains(t) || v.Cliente.Apellido.Contains(t))));
        }

        var total = await query.CountAsync();
        pagina = Paginacion.Ajustar(pagina, total, tamPagina);
        var elementos = await query
            .OrderBy(v => v.Placa)
            .Skip((pagina - 1) * tamPagina).Take(tamPagina)
            .ToListAsync();

        return new ResultadoPaginado<Vehiculo>(elementos, total, pagina, tamPagina);
    }

    public Task<Vehiculo?> ObtenerPorId(int idVehiculo) =>
        _db.Vehiculos.AsNoTracking().Include(v => v.Cliente)
            .FirstOrDefaultAsync(v => v.IdVehiculo == idVehiculo);

    /// <summary>Historial completo del vehículo: servicios, trabajadores, productos y facturas (Regla 3).</summary>
    public async Task<List<ItemHistorial>> ObtenerHistorial(int idVehiculo)
    {
        var servicios = await _db.Servicios.AsNoTracking()
            .Where(s => s.IdVehiculo == idVehiculo)
            .Include(s => s.DetalleServicios).ThenInclude(d => d.Trabajador)
            .Include(s => s.ProductosUtilizados).ThenInclude(pu => pu.Producto)
            .Include(s => s.DetalleFacturas).ThenInclude(df => df.Factura)
            .OrderByDescending(s => s.FechaInicio)
            .ThenByDescending(s => s.IdServicio)
            .ToListAsync();

        return servicios.Select(s => new ItemHistorial(
            s.IdServicio,
            s.FechaInicio,
            s.FechaFin,
            s.TipoServicio,
            s.Descripcion,
            s.Estado,
            s.Total,
            s.DetalleServicios
                .Where(d => d.Trabajador != null)
                .Select(d => $"{d.Trabajador!.NombreCompleto} ({d.Estado})").ToList(),
            s.ProductosUtilizados
                .Where(pu => pu.Producto != null)
                .Select(pu => $"{pu.Producto!.Nombre} x {pu.Cantidad:0.###}").ToList(),
            s.DetalleFacturas
                .Where(df => df.Factura != null)
                .Select(df => $"{df.Factura!.NumeroFactura} ({df.Factura.Estado})").ToList()
        )).ToList();
    }

}
