using Microsoft.EntityFrameworkCore;
using TallerMecanicoSantoDomingo.Data;
using TallerMecanicoSantoDomingo.Models;

namespace TallerMecanicoSantoDomingo.Services;

/// <summary>Uso histórico de un producto en los servicios.</summary>
public record ItemUsoProducto(int IdServicio, string DescripcionServicio, string PlacaVehiculo,
    DateOnly FechaServicio, decimal Cantidad);

/// <summary>
/// Servicio de negocio para PRODUCTOS (MER: REPUESTO) y PRODUCTOS_UTILIZADOS (MER: UTILIZA).
/// Reglas aplicadas: 9 (pareza producto-servicio única) y 10 (cantidades mayores
/// que cero; precios no negativos). Los consumos y Stock se guardan en una transacción.
/// </summary>
public class ProductoService
{
    private readonly TallerDbContext _db;

    public ProductoService(TallerDbContext db) => _db = db;

    public Task<Resultado> Crear(Producto producto) => OperacionTaller.Ejecutar(_db, async () =>
    {
        if (producto.IdProducto != 0) return Resultado.Fallo("El identificador del producto se genera automáticamente.");
        if (Validar(producto) is { } error) return Resultado.Fallo(error);
        producto.FechaRegistro = DateTime.UtcNow;
        producto.Activo = true;
        producto.ProductosUtilizados = [];
        _db.Productos.Add(producto);
        await _db.SaveChangesAsync();
        return Resultado.Ok("Producto registrado correctamente.");
    }, Resultado.Fallo);

    public Task<Resultado> Editar(Producto producto, decimal? stockOriginal) => OperacionTaller.Ejecutar(_db, async () =>
    {
        var existente = await _db.Productos.SingleOrDefaultAsync(p => p.IdProducto == producto.IdProducto);
        if (existente is null) return Resultado.Fallo("El producto indicado no existe.");
        if (!stockOriginal.HasValue || !ReglasTaller.Cantidad(stockOriginal.Value))
            return Resultado.Fallo("Recargue el formulario antes de editar el producto; falta el saldo original de existencias.");
        if (existente.Stock != stockOriginal.Value)
            return Resultado.Fallo("Las existencias cambiaron mientras editaba. No se guardaron cambios; recargue el formulario y confirme el nuevo saldo.");
        if (Validar(producto) is { } error) return Resultado.Fallo(error);
        existente.Nombre = producto.Nombre;
        existente.Descripcion = producto.Descripcion;
        existente.PrecioUnitario = producto.PrecioUnitario;
        existente.Costo = producto.Costo;
        existente.Stock = producto.Stock;
        await _db.SaveChangesAsync();
        return Resultado.Ok("Producto actualizado correctamente.");
    }, Resultado.Fallo);

    private static string? Validar(Producto producto)
    {
        producto.Nombre = producto.Nombre?.Trim() ?? "";
        producto.Descripcion = producto.Descripcion?.Trim() ?? "";
        if (ReglasTaller.Campos(producto) is { } error) return error;
        if (!ReglasTaller.Importe(producto.PrecioUnitario) || !ReglasTaller.Importe(producto.Costo))
            return "El precio unitario y el costo deben ser no negativos, con máximo dos decimales y compatibles con la base de datos.";
        if (!ReglasTaller.Cantidad(producto.Stock)) return "Las existencias deben ser no negativas, con máximo tres decimales y compatibles con la base de datos.";
        return null;
    }

    public Task<Resultado> CambiarEstado(int idProducto, bool activo) => OperacionTaller.Ejecutar(_db, async () =>
    {
        var producto = await _db.Productos.SingleOrDefaultAsync(p => p.IdProducto == idProducto);
        if (producto is null) return Resultado.Fallo("El producto indicado no existe.");
        producto.Activo = activo;
        await _db.SaveChangesAsync();
        return Resultado.Ok(activo ? "Producto reactivado correctamente." : "Producto desactivado.");
    }, Resultado.Fallo);

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
        pagina = Paginacion.Ajustar(pagina, total, tamPagina);
        var elementos = await query
            .OrderBy(p => p.Nombre).ThenBy(p => p.IdProducto)
            .Skip((pagina - 1) * tamPagina).Take(tamPagina)
            .ToListAsync();

        return new ResultadoPaginado<Producto>(elementos, total, pagina, tamPagina);
    }

    public Task<Producto?> ObtenerPorId(int idProducto) =>
        _db.Productos.AsNoTracking().FirstOrDefaultAsync(p => p.IdProducto == idProducto);

    public Task<List<Producto>> ListarActivosParaSeleccion() =>
        _db.Productos.AsNoTracking().Where(p => p.Activo).OrderBy(p => p.Nombre).ToListAsync();

    public Task<Resultado> AgregarAlServicio(int idServicio, int idProducto, decimal cantidad) =>
        OperacionTaller.Ejecutar(_db, async () =>
        {
            if (cantidad <= 0 || !ReglasTaller.Cantidad(cantidad)) return Resultado.Fallo("La cantidad debe ser mayor que cero y admitir como máximo tres decimales dentro del rango de la base de datos.");
            if (await ValidarServicio(idServicio) is { } error) return Resultado.Fallo(error);
            var producto = await _db.Productos.SingleOrDefaultAsync(p => p.IdProducto == idProducto);
            if (producto is null) return Resultado.Fallo("El producto indicado no existe.");
            if (!producto.Activo) return Resultado.Fallo("El producto está desactivado y no admite nuevos consumos.");
            if (await _db.ProductosUtilizados.AnyAsync(p => p.IdProducto == idProducto && p.IdServicio == idServicio))
                return Resultado.Fallo("Este producto ya está registrado en el servicio. Modifique la cantidad en lugar de duplicarlo.");
            if (!await CambiarStock(producto, cantidad)) return Resultado.Fallo("No hay existencias suficientes.");
            _db.ProductosUtilizados.Add(new ProductoUtilizado { IdProducto = idProducto, IdServicio = idServicio, Cantidad = cantidad });
            await _db.SaveChangesAsync();
            return Resultado.Ok("Producto agregado al servicio y existencias actualizadas.");
        }, Resultado.Fallo);

    public Task<Resultado> ActualizarCantidad(int idServicio, int idProducto, decimal cantidad) =>
        OperacionTaller.Ejecutar(_db, async () =>
        {
            if (cantidad <= 0 || !ReglasTaller.Cantidad(cantidad)) return Resultado.Fallo("La cantidad debe ser mayor que cero y admitir como máximo tres decimales dentro del rango de la base de datos.");
            if (await ValidarServicio(idServicio) is { } error) return Resultado.Fallo(error);
            var registro = await _db.ProductosUtilizados.SingleOrDefaultAsync(p => p.IdProducto == idProducto && p.IdServicio == idServicio);
            if (registro is null) return Resultado.Fallo("El registro de producto indicado no existe.");
            var producto = await _db.Productos.SingleOrDefaultAsync(p => p.IdProducto == idProducto);
            if (producto is null) return Resultado.Fallo("El producto indicado no existe.");
            var diferencia = cantidad - registro.Cantidad;
            if (diferencia > 0 && !producto.Activo) return Resultado.Fallo("El producto está desactivado y no admite nuevos consumos.");
            if (!await CambiarStock(producto, diferencia)) return Resultado.Fallo("No hay existencias suficientes o la devolución supera el rango de la base de datos.");
            registro.Cantidad = cantidad;
            await _db.SaveChangesAsync();
            return Resultado.Ok("Cantidad y existencias actualizadas correctamente.");
        }, Resultado.Fallo);

    public Task<Resultado> QuitarDelServicio(int idServicio, int idProducto) => OperacionTaller.Ejecutar(_db, async () =>
    {
        if (await ValidarServicio(idServicio) is { } error) return Resultado.Fallo(error);
        if (await _db.DetalleFactura.AnyAsync(d => d.IdServicio == idServicio))
            return Resultado.Fallo("El consumo forma parte del historial de una factura y no puede eliminarse.");
        var registro = await _db.ProductosUtilizados.SingleOrDefaultAsync(p => p.IdProducto == idProducto && p.IdServicio == idServicio);
        if (registro is null) return Resultado.Fallo("El consumo ya fue retirado o no existe; no se modificaron las existencias.");
        var producto = await _db.Productos.SingleOrDefaultAsync(p => p.IdProducto == idProducto);
        if (producto is null || !await CambiarStock(producto, -registro.Cantidad)) return Resultado.Fallo("No se pudo devolver el consumo a las existencias.");
        _db.ProductosUtilizados.Remove(registro);
        await _db.SaveChangesAsync();
        return Resultado.Ok("Consumo corregido y existencias devueltas explícitamente.");
    }, Resultado.Fallo);

    private async Task<string?> ValidarServicio(int idServicio)
    {
        var servicio = await _db.Servicios.SingleOrDefaultAsync(s => s.IdServicio == idServicio);
        if (servicio is null) return "El servicio indicado no existe.";
        if (await ReglasTaller.Facturado(_db, idServicio)) return "Este servicio ya tiene una factura vigente; anúlela antes de corregir los consumos.";
        if (servicio.Estado != ReglasTaller.Abierto) return "Solo se pueden corregir consumos de servicios abiertos.";
        if (!ReglasTaller.TipoValido(servicio.TipoServicio)) return "El servicio tiene un tipo desconocido.";
        // Pintura ya admitía consumos: se conserva ese flujo y se documenta la discrepancia del alcance.
        return null;
    }

    private async Task<bool> CambiarStock(Producto producto, decimal diferencia)
    {
        if (producto.Stock < diferencia || !ReglasTaller.Cantidad(producto.Stock - diferencia)) return false;
        if (_db.Database.IsSqlServer())
        {
            var filas = await _db.Productos.Where(p => p.IdProducto == producto.IdProducto && p.Stock >= diferencia &&
                    p.Stock - diferencia <= ReglasTaller.MaxCantidad && (diferencia <= 0 || p.Activo))
                .ExecuteUpdateAsync(set => set.SetProperty(p => p.Stock, p => p.Stock - diferencia));
            if (filas != 1) return false;
            await _db.Entry(producto).ReloadAsync();
        }
        else producto.Stock -= diferencia;
        return true;
    }

    public async Task<List<ItemUsoProducto>> HistorialDelProducto(int idProducto)
    {
        var usos = await _db.ProductosUtilizados.AsNoTracking()
            .Where(pu => pu.IdProducto == idProducto)
            .Include(pu => pu.Servicio).ThenInclude(s => s!.Vehiculo)
            .OrderByDescending(pu => pu.Servicio!.FechaInicio)
            .ToListAsync();

        return usos.Select(pu => new ItemUsoProducto(
            pu.IdServicio,
            pu.Servicio?.Descripcion ?? "",
            pu.Servicio?.Vehiculo?.Placa ?? "",
            pu.Servicio?.FechaInicio ?? default,
            pu.Cantidad)).ToList();
    }
}
