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

    public async Task<Resultado> Crear(Vehiculo vehiculo)
    {
        vehiculo.Placa = vehiculo.Placa.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(vehiculo.Placa))
            return Resultado.Fallo("La placa del vehículo es obligatoria.");

        // Regla 2: el vehículo debe asociarse a un cliente existente.
        var clienteExiste = await _db.Clientes.AnyAsync(c => c.IdCliente == vehiculo.IdCliente);
        if (!clienteExiste)
            return Resultado.Fallo("El cliente seleccionado no existe. Registre el cliente primero.");

        var placaDuplicada = await _db.Vehiculos.AnyAsync(v => v.Placa == vehiculo.Placa);
        if (placaDuplicada)
            return Resultado.Fallo($"Ya existe un vehículo registrado con la placa {vehiculo.Placa}.");

        if (vehiculo.Anio < 1950 || vehiculo.Anio > DateTime.Now.Year + 1)
            return Resultado.Fallo("El año del vehículo no es válido.");

        vehiculo.FechaRegistro = DateTime.UtcNow;
        vehiculo.Activo = true;
        _db.Vehiculos.Add(vehiculo);
        await _db.SaveChangesAsync();
        return Resultado.Ok("Vehículo registrado correctamente.");
    }

    public async Task<Resultado> Editar(Vehiculo vehiculo)
    {
        var existente = await _db.Vehiculos.FindAsync(vehiculo.IdVehiculo);
        if (existente is null)
            return Resultado.Fallo("El vehículo indicado no existe.");

        vehiculo.Placa = vehiculo.Placa.Trim().ToUpperInvariant();
        var clienteExiste = await _db.Clientes.AnyAsync(c => c.IdCliente == vehiculo.IdCliente);
        if (!clienteExiste)
            return Resultado.Fallo("El cliente seleccionado no existe.");

        var placaDuplicada = await _db.Vehiculos
            .AnyAsync(v => v.Placa == vehiculo.Placa && v.IdVehiculo != vehiculo.IdVehiculo);
        if (placaDuplicada)
            return Resultado.Fallo($"Ya existe otro vehículo registrado con la placa {vehiculo.Placa}.");

        if (vehiculo.Anio < 1950 || vehiculo.Anio > DateTime.Now.Year + 1)
            return Resultado.Fallo("El año del vehículo no es válido.");

        existente.IdCliente = vehiculo.IdCliente;
        existente.Placa = vehiculo.Placa;
        existente.Marca = vehiculo.Marca.Trim();
        existente.Modelo = vehiculo.Modelo.Trim();
        existente.Anio = vehiculo.Anio;
        await _db.SaveChangesAsync();
        return Resultado.Ok("Vehículo actualizado correctamente.");
    }

    /// <summary>Regla 14: los vehículos con servicios no se eliminan; se desactivan.</summary>
    public async Task<Resultado> CambiarEstado(int idVehiculo, bool activo)
    {
        var vehiculo = await _db.Vehiculos.FindAsync(idVehiculo);
        if (vehiculo is null)
            return Resultado.Fallo("El vehículo indicado no existe.");

        if (!activo)
        {
            var tieneServiciosAbiertos = await _db.Servicios
                .AnyAsync(s => s.IdVehiculo == idVehiculo && s.Estado == "Abierto");
            if (tieneServiciosAbiertos)
                return Resultado.Fallo("No se puede desactivar el vehículo porque tiene servicios abiertos.");
        }

        vehiculo.Activo = activo;
        await _db.SaveChangesAsync();
        return Resultado.Ok(activo ? "Vehículo reactivado correctamente." : "Vehículo desactivado.");
    }

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
