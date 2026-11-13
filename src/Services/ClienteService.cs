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
public class ClienteService
{
    private readonly TallerDbContext _db;

    public ClienteService(TallerDbContext db) => _db = db;

    public Task<Resultado> Crear(Cliente cliente) => OperacionTaller.Ejecutar(_db, async () =>
    {
        if (cliente.IdCliente != 0) return Resultado.Fallo("El identificador del cliente se genera automáticamente.");
        var error = await Validar(cliente);
        if (error is not null)
            return error;

        cliente.FechaRegistro = DateTime.UtcNow;
        cliente.Activo = true;
        cliente.Vehiculos = [];
        cliente.Facturas = [];
        _db.Clientes.Add(cliente);
        await _db.SaveChangesAsync();
        return Resultado.Ok("Cliente registrado correctamente.");
    }, Resultado.Fallo);

    public Task<Resultado> Editar(Cliente cliente) => OperacionTaller.Ejecutar(_db, async () =>
    {
        var existente = await _db.Clientes.FindAsync(cliente.IdCliente);
        if (existente is null)
            return Resultado.Fallo("El cliente indicado no existe.");

        var error = await Validar(cliente);
        if (error is not null)
            return error;

        existente.Nombre = cliente.Nombre;
        existente.Apellido = cliente.Apellido;
        existente.Telefono = cliente.Telefono;
        existente.Direccion = cliente.Direccion;
        existente.RFC = cliente.RFC;
        existente.Cedula = cliente.Cedula;
        await _db.SaveChangesAsync();
        return Resultado.Ok("Cliente actualizado correctamente.");
    }, Resultado.Fallo);

    private async Task<Resultado?> Validar(Cliente cliente)
    {
        cliente.Nombre = cliente.Nombre?.Trim() ?? "";
        cliente.Apellido = cliente.Apellido?.Trim() ?? "";
        cliente.RFC = string.IsNullOrWhiteSpace(cliente.RFC) ? null : cliente.RFC.Trim().ToUpperInvariant();
        cliente.Telefono = cliente.Telefono?.Trim() ?? "";
        cliente.Direccion = cliente.Direccion?.Trim() ?? "";
        cliente.Cedula = string.IsNullOrWhiteSpace(cliente.Cedula) ? null : cliente.Cedula.Trim().ToUpperInvariant();

        if (ReglasTaller.Campos(cliente) is { } campos) return Resultado.Fallo(campos);
        if (!new System.ComponentModel.DataAnnotations.PhoneAttribute().IsValid(cliente.Telefono))
            return Resultado.Fallo("Debe ingresar un teléfono válido.");
        if (cliente.Nombre.Length == 0 || cliente.Apellido.Length == 0)
            return Resultado.Fallo("El nombre y el apellido del cliente son obligatorios.");
        if (cliente.Cedula is null && cliente.RFC is null)
            return Resultado.Fallo("Ingrese la cédula del cliente o el RUC de la empresa.");

        var otros = _db.Clientes.Where(c => c.IdCliente != cliente.IdCliente);
        if (cliente.RFC is not null && await otros.AnyAsync(c => c.RFC == cliente.RFC))
            return Resultado.Fallo($"Ya existe un cliente registrado con el RUC {cliente.RFC}.");
        if (cliente.Cedula is not null && await otros.AnyAsync(c => c.Cedula == cliente.Cedula))
            return Resultado.Fallo($"Ya existe un cliente registrado con la cédula {cliente.Cedula}.");

        return null;
    }

    /// <summary>Regla 14: desactivación lógica; nunca borrado físico.</summary>
    public Task<Resultado> CambiarEstado(int idCliente, bool activo) => OperacionTaller.Ejecutar(_db, async () =>
    {
        var cliente = await _db.Clientes.FindAsync(idCliente);
        if (cliente is null)
            return Resultado.Fallo("El cliente indicado no existe.");

        cliente.Activo = activo;
        await _db.SaveChangesAsync();
        return Resultado.Ok(activo ? "Cliente reactivado correctamente." : "Cliente desactivado.");
    }, Resultado.Fallo);

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
                (c.RFC != null && c.RFC.Contains(t)) || c.Telefono.Contains(t) ||
                (c.Cedula != null && c.Cedula.Contains(t)));
        }

        var total = await query.CountAsync();
        pagina = Paginacion.Ajustar(pagina, total, tamPagina);
        var elementos = await query
            .OrderBy(c => c.Apellido).ThenBy(c => c.Nombre).ThenBy(c => c.IdCliente)
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
            .Where(f => f.IdCliente == idCliente && f.Estado == "Emitida")
            .SumAsync(f => (decimal?)f.Total) ?? 0m;
        return Resultado<decimal>.Ok(total);
    }
}
