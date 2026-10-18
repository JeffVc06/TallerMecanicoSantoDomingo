using Microsoft.EntityFrameworkCore;
using TallerMecanicoSantoDomingo.Data;
using TallerMecanicoSantoDomingo.Models;

namespace TallerMecanicoSantoDomingo.Services;

/// <summary>Resultado paginado genérico para listados.</summary>
public record ResultadoPaginado<T>(List<T> Elementos, int TotalElementos, int Pagina, int TamPagina)
{
    public int TotalPaginas => Math.Max(1, (int)Math.Ceiling(TotalElementos / (double)TamPagina));
}

/// <summary>
/// Servicio de negocio para la entidad CLIENTE.
/// Reglas aplicadas: 1 (identificador único e integridad referencial) y
/// 14 (no se elimina físicamente; se desactiva lógicamente).
/// </summary>
public interface IClienteService
{
    Task<Resultado> Crear(Cliente cliente);
    Task<Resultado> Editar(Cliente cliente);
    Task<Resultado> CambiarEstado(int idCliente, bool activo);
    Task<ResultadoPaginado<Cliente>> Buscar(string? termino, bool? soloActivos, int pagina, int tamPagina);
    Task<Cliente?> ObtenerPorId(int idCliente);
    Task<List<Vehiculo>> ObtenerVehiculosDelCliente(int idCliente);
    Task<Resultado<decimal>> TotalFacturadoDelCliente(int idCliente);
}

public class ClienteService : IClienteService
{
    private readonly TallerDbContext _db;

    public ClienteService(TallerDbContext db) => _db = db;

    public async Task<Resultado> Crear(Cliente cliente)
    {
        cliente.Nombre = cliente.Nombre.Trim();
        cliente.Apellido = cliente.Apellido.Trim();
        cliente.RFC = cliente.RFC.Trim().ToUpperInvariant();
        cliente.Telefono = cliente.Telefono.Trim();
        cliente.Direccion = cliente.Direccion.Trim();

        if (string.IsNullOrWhiteSpace(cliente.Nombre) || string.IsNullOrWhiteSpace(cliente.Apellido))
            return Resultado.Fallo("El nombre y el apellido del cliente son obligatorios.");
        if (string.IsNullOrWhiteSpace(cliente.RFC))
            return Resultado.Fallo("El RFC (identificación) del cliente es obligatorio.");

        var rfcDuplicado = await _db.Clientes.AnyAsync(c => c.RFC == cliente.RFC);
        if (rfcDuplicado)
            return Resultado.Fallo($"Ya existe un cliente registrado con el RFC {cliente.RFC}.");

        cliente.FechaRegistro = DateTime.UtcNow;
        cliente.Activo = true;
        _db.Clientes.Add(cliente);
        await _db.SaveChangesAsync();
        return Resultado.Ok("Cliente registrado correctamente.");
    }

    public async Task<Resultado> Editar(Cliente cliente)
    {
        var existente = await _db.Clientes.FindAsync(cliente.IdCliente);
        if (existente is null)
            return Resultado.Fallo("El cliente indicado no existe.");

        cliente.Nombre = cliente.Nombre.Trim();
        cliente.Apellido = cliente.Apellido.Trim();
        cliente.RFC = cliente.RFC.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(cliente.Nombre) || string.IsNullOrWhiteSpace(cliente.Apellido))
            return Resultado.Fallo("El nombre y el apellido del cliente son obligatorios.");
        if (string.IsNullOrWhiteSpace(cliente.RFC))
            return Resultado.Fallo("El RFC (identificación) del cliente es obligatorio.");

        var rfcDuplicado = await _db.Clientes
            .AnyAsync(c => c.RFC == cliente.RFC && c.IdCliente != cliente.IdCliente);
        if (rfcDuplicado)
            return Resultado.Fallo($"Ya existe otro cliente registrado con el RFC {cliente.RFC}.");

        existente.Nombre = cliente.Nombre;
        existente.Apellido = cliente.Apellido;
        existente.Telefono = cliente.Telefono.Trim();
        existente.Direccion = cliente.Direccion.Trim();
        existente.RFC = cliente.RFC;
        await _db.SaveChangesAsync();
        return Resultado.Ok("Cliente actualizado correctamente.");
    }

    /// <summary>Regla 14: desactivación lógica; nunca borrado físico.</summary>
    public async Task<Resultado> CambiarEstado(int idCliente, bool activo)
    {
        var cliente = await _db.Clientes.FindAsync(idCliente);
        if (cliente is null)
            return Resultado.Fallo("El cliente indicado no existe.");

        cliente.Activo = activo;
        await _db.SaveChangesAsync();
        return Resultado.Ok(activo ? "Cliente reactivado correctamente." : "Cliente desactivado. Su historial se conserva.");
    }

    public async Task<ResultadoPaginado<Cliente>> Buscar(string? termino, bool? soloActivos, int pagina, int tamPagina)
    {
        pagina = Math.Max(1, pagina);
        tamPagina = Math.Clamp(tamPagina, 5, 200);

        var query = _db.Clientes.AsNoTracking();
        if (soloActivos == true)
            query = query.Where(c => c.Activo);
        if (!string.IsNullOrWhiteSpace(termino))
        {
            var t = termino.Trim();
            query = query.Where(c =>
                c.Nombre.Contains(t) || c.Apellido.Contains(t) ||
                c.RFC.Contains(t) || c.Telefono.Contains(t));
        }

        var total = await query.CountAsync();
        var elementos = await query
            .OrderBy(c => c.Apellido).ThenBy(c => c.Nombre)
            .Skip((pagina - 1) * tamPagina).Take(tamPagina)
            .ToListAsync();

        return new ResultadoPaginado<Cliente>(elementos, total, pagina, tamPagina);
    }

    public Task<Cliente?> ObtenerPorId(int idCliente) =>
        _db.Clientes.AsNoTracking()
            .Include(c => c.Facturas)
            .FirstOrDefaultAsync(c => c.IdCliente == idCliente);

    public Task<List<Vehiculo>> ObtenerVehiculosDelCliente(int idCliente) =>
        _db.Vehiculos.AsNoTracking()
            .Where(v => v.IdCliente == idCliente)
            .OrderBy(v => v.Placa)
            .ToListAsync();

    public async Task<Resultado<decimal>> TotalFacturadoDelCliente(int idCliente)
    {
        var total = await _db.Facturas.AsNoTracking()
            .Where(f => f.IdCliente == idCliente && f.Estado == ValoresCatalogo.FacturaEmitida)
            .SumAsync(f => (decimal?)f.Total) ?? 0m;
        return Resultado<decimal>.Ok(total);
    }
}
