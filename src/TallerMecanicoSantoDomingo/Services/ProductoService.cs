using Microsoft.EntityFrameworkCore;
using TallerMecanicoSantoDomingo.Data;
using TallerMecanicoSantoDomingo.Models;

namespace TallerMecanicoSantoDomingo.Services;

/// <summary>Uso histórico de un producto en los servicios.</summary>
public record ItemUsoProducto(int IdProductoUtilizado, int IdServicio, string DescripcionServicio, string PlacaVehiculo,
    DateOnly FechaServicio, decimal Cantidad);

/// <summary>
/// Servicio de negocio para PRODUCTOS (MER: REPUESTO) y PRODUCTOS_UTILIZADOS (MER: UTILIZA).
/// Reglas aplicadas: 9 (pareza producto-servicio única) y 10 (cantidades mayores
/// que cero; precios no negativos). El catálogo conserva el atributo Stock del
/// documento original, sin implementar un módulo de inventario adicional.
/// </summary>
public interface IProductoService
{
    Task<Resultado> Crear(Producto producto);
    Task<Resultado> Editar(Producto producto);
    Task<Resultado> CambiarEstado(int idProducto, bool activo);
    Task<ResultadoPaginado<Producto>> Buscar(string? termino, bool? soloActivos, int pagina, int tamPagina);
    Task<Producto?> ObtenerPorId(int idProducto);
    Task<List<Producto>> ListarActivosParaSeleccion();

    Task<Resultado> AgregarAlServicio(int idServicio, int idProducto, decimal cantidad);
    Task<Resultado> ActualizarCantidad(int idProductoUtilizado, decimal cantidad);
    Task<Resultado> QuitarDelServicio(int idProductoUtilizado);
    Task<List<ItemUsoProducto>> HistorialDelProducto(int idProducto);
}

public class ProductoService : IProductoService
{
    private readonly TallerDbContext _db;

    public ProductoService(TallerDbContext db) => _db = db;

    public async Task<Resultado> Crear(Producto producto)
    {
        producto.Nombre = producto.Nombre.Trim();
        producto.Descripcion = producto.Descripcion.Trim();

        if (string.IsNullOrWhiteSpace(producto.Nombre))
            return Resultado.Fallo("El nombre del producto es obligatorio.");
        if (producto.PrecioUnitario < 0)
            return Resultado.Fallo("El precio unitario no puede ser negativo (Regla 10).");
        if (producto.Stock < 0)
            return Resultado.Fallo("El stock no puede ser negativo (Regla 10).");

        producto.FechaRegistro = DateTime.UtcNow;
        producto.Activo = true;
        _db.Productos.Add(producto);
        await _db.SaveChangesAsync();
        return Resultado.Ok("Producto registrado correctamente.");
    }

    public async Task<Resultado> Editar(Producto producto)
    {
        var existente = await _db.Productos.FindAsync(producto.IdProducto);
        if (existente is null)
            return Resultado.Fallo("El producto indicado no existe.");

        producto.Nombre = producto.Nombre.Trim();
        if (string.IsNullOrWhiteSpace(producto.Nombre))
            return Resultado.Fallo("El nombre del producto es obligatorio.");
        if (producto.PrecioUnitario < 0)
            return Resultado.Fallo("El precio unitario no puede ser negativo (Regla 10).");
        if (producto.Stock < 0)
            return Resultado.Fallo("El stock no puede ser negativo (Regla 10).");

        existente.Nombre = producto.Nombre;
        existente.Descripcion = producto.Descripcion.Trim();
        existente.PrecioUnitario = producto.PrecioUnitario;
        existente.Stock = producto.Stock;
        await _db.SaveChangesAsync();
        return Resultado.Ok("Producto actualizado correctamente.");
    }

    public async Task<Resultado> CambiarEstado(int idProducto, bool activo)
    {
        var producto = await _db.Productos.FindAsync(idProducto);
        if (producto is null)
            return Resultado.Fallo("El producto indicado no existe.");

        producto.Activo = activo;
        await _db.SaveChangesAsync();
        return Resultado.Ok(activo ? "Producto reactivado correctamente." : "Producto desactivado. Su historial de uso se conserva.");
    }

    public async Task<ResultadoPaginado<Producto>> Buscar(string? termino, bool? soloActivos, int pagina, int tamPagina)
    {
        pagina = Math.Max(1, pagina);
        tamPagina = Math.Clamp(tamPagina, 5, 200);

        var query = _db.Productos.AsNoTracking();
        if (soloActivos == true)
            query = query.Where(p => p.Activo);
        if (!string.IsNullOrWhiteSpace(termino))
        {
            var t = termino.Trim();
            query = query.Where(p => p.Nombre.Contains(t) || p.Descripcion.Contains(t));
        }

        var total = await query.CountAsync();
        var elementos = await query
            .OrderBy(p => p.Nombre)
            .Skip((pagina - 1) * tamPagina).Take(tamPagina)
            .ToListAsync();

        return new ResultadoPaginado<Producto>(elementos, total, pagina, tamPagina);
    }

    public Task<Producto?> ObtenerPorId(int idProducto) =>
        _db.Productos.AsNoTracking().FirstOrDefaultAsync(p => p.IdProducto == idProducto);

    public Task<List<Producto>> ListarActivosParaSeleccion() =>
        _db.Productos.AsNoTracking().Where(p => p.Activo).OrderBy(p => p.Nombre).ToListAsync();

    /// <summary>Reglas 9 y 10: la pareja producto-servicio no se repite y la cantidad debe ser mayor que cero.</summary>
    public async Task<Resultado> AgregarAlServicio(int idServicio, int idProducto, decimal cantidad)
    {
        if (cantidad <= 0)
            return Resultado.Fallo("La cantidad utilizada debe ser mayor que cero (Regla 10).");

        var servicio = await _db.Servicios.FindAsync(idServicio);
        if (servicio is null)
            return Resultado.Fallo("El servicio indicado no existe.");
        if (servicio.Estado != "Abierto")
            return Resultado.Fallo("Solo se pueden agregar productos a servicios abiertos.");

        var producto = await _db.Productos.FindAsync(idProducto);
        if (producto is null)
            return Resultado.Fallo("El producto indicado no existe.");
        if (!producto.Activo)
            return Resultado.Fallo("El producto está desactivado y no puede agregarse al servicio.");

        var duplicado = await _db.ProductosUtilizados
            .AnyAsync(pu => pu.IdServicio == idServicio && pu.IdProducto == idProducto);
        if (duplicado)
            return Resultado.Fallo("Este producto ya está registrado en el servicio. Modifique la cantidad en lugar de duplicarlo (Regla 9).");

        _db.ProductosUtilizados.Add(new ProductoUtilizado
        {
            IdServicio = idServicio,
            IdProducto = idProducto,
            Cantidad = cantidad
        });
        await _db.SaveChangesAsync();
        return Resultado.Ok("Producto agregado al servicio.");
    }

    public async Task<Resultado> ActualizarCantidad(int idProductoUtilizado, decimal cantidad)
    {
        if (cantidad <= 0)
            return Resultado.Fallo("La cantidad utilizada debe ser mayor que cero (Regla 10).");

        var registro = await _db.ProductosUtilizados
            .Include(pu => pu.Servicio)
            .FirstOrDefaultAsync(pu => pu.IdProductoUtilizado == idProductoUtilizado);
        if (registro is null)
            return Resultado.Fallo("El registro de producto indicado no existe.");
        if (registro.Servicio?.Estado != "Abierto")
            return Resultado.Fallo("Solo se pueden modificar productos de servicios abiertos.");

        registro.Cantidad = cantidad;
        await _db.SaveChangesAsync();
        return Resultado.Ok("Cantidad actualizada correctamente.");
    }

    public async Task<Resultado> QuitarDelServicio(int idProductoUtilizado)
    {
        var registro = await _db.ProductosUtilizados
            .Include(pu => pu.Servicio)
            .FirstOrDefaultAsync(pu => pu.IdProductoUtilizado == idProductoUtilizado);
        if (registro is null)
            return Resultado.Fallo("El registro de producto indicado no existe.");
        if (registro.Servicio?.Estado != "Abierto")
            return Resultado.Fallo("Solo se pueden quitar productos de servicios abiertos.");

        _db.ProductosUtilizados.Remove(registro);
        await _db.SaveChangesAsync();
        return Resultado.Ok("Producto quitado del servicio.");
    }

    public async Task<List<ItemUsoProducto>> HistorialDelProducto(int idProducto)
    {
        var usos = await _db.ProductosUtilizados.AsNoTracking()
            .Where(pu => pu.IdProducto == idProducto)
            .Include(pu => pu.Servicio).ThenInclude(s => s!.Vehiculo)
            .OrderByDescending(pu => pu.Servicio!.FechaInicio)
            .ToListAsync();

        return usos.Select(pu => new ItemUsoProducto(
            pu.IdProductoUtilizado,
            pu.IdServicio,
            pu.Servicio?.Descripcion ?? "",
            pu.Servicio?.Vehiculo?.Placa ?? "",
            pu.Servicio?.FechaInicio ?? default,
            pu.Cantidad)).ToList();
    }
}
