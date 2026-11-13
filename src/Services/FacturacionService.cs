using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TallerMecanicoSantoDomingo.Data;
using TallerMecanicoSantoDomingo.Models;

namespace TallerMecanicoSantoDomingo.Services;

/// <summary>Servicio elegible para facturación con el cliente propietario.</summary>
public record ServicioElegible(int IdServicio, int IdVehiculo, string Placa, string TipoServicio, string Descripcion,
    DateOnly FechaInicio, string Estado, decimal Total);

/// <summary>Detalle completo de una factura para consulta e impresión.</summary>
public record FacturaCompleta(Factura Factura, List<DetalleFacturaLinea> Detalles);

public record DetalleFacturaLinea(int IdServicio, string TipoServicio, string Descripcion, string Placa,
    DateOnly FechaInicio, decimal Subtotal);

/// <summary>
/// Servicio de negocio de facturación (MER: FACTURA y DETALLE_FACTURA).
/// Reglas aplicadas: 11 (al menos un detalle; pareja factura-servicio única),
/// 12 (todos los servicios pertenecen al mismo cliente), 13 (totales calculados
/// en el servidor: Total = Subtotal + IVA, dentro de una transacción SQL) y
/// 14 (anulación controlada sin destruir historial).
/// </summary>
public class FacturacionService
{
    private readonly TallerDbContext _db;
    private readonly ConfiguracionTaller _config;

    public FacturacionService(TallerDbContext db, IOptions<ConfiguracionTaller> config)
    {
        _db = db;
        _config = config.Value;
    }

    /// <summary>
    /// Crea la factura y sus detalles dentro de una transacción SQL.
    /// El navegador nunca envía importes: el backend recalcula subtotal, IVA y total
    /// a partir de los servicios almacenados (Regla 13).
    /// </summary>
    public Task<Resultado<Factura>> CrearFactura(int idCliente, string metodoPago, List<int> idsServicios, string? notas) =>
        OperacionTaller.Ejecutar(_db, async () =>
        {
            var ids = (idsServicios ?? []).ToList();
            if (ids.Count == 0) return Resultado<Factura>.Fallo("Debe seleccionar al menos un servicio para facturar.");
            if (ids.Distinct().Count() != ids.Count) return Resultado<Factura>.Fallo(ReglasTaller.ErrorServiciosRepetidos);
            if (ids.Any(id => id <= 0)) return Resultado<Factura>.Fallo("Uno o más servicios seleccionados no existen.");
            if (metodoPago is not ("Efectivo" or "Tarjeta" or "Transferencia")) return Resultado<Factura>.Fallo("El método de pago no es válido.");
            notas = ReglasTaller.Opcional(notas);
            if (notas?.Length > 200) return Resultado<Factura>.Fallo("Las notas no pueden exceder 200 caracteres.");
            if (_config.PorcentajeIVA < 0) return Resultado<Factura>.Fallo("La configuración del IVA no es válida.");
            var cliente = await _db.Clientes.SingleOrDefaultAsync(c => c.IdCliente == idCliente);
            if (cliente is null) return Resultado<Factura>.Fallo("El cliente indicado no existe.");
            if (!cliente.Activo) return Resultado<Factura>.Fallo("El cliente está desactivado y no puede recibir facturas nuevas.");
            var servicios = await _db.Servicios.Include(s => s.Vehiculo).Where(s => ids.Contains(s.IdServicio)).ToListAsync();
            if (servicios.Count != ids.Count) return Resultado<Factura>.Fallo("Uno o más servicios seleccionados no existen.");
            foreach (var servicio in servicios)
            {
                if (servicio.Estado != ReglasTaller.Finalizado) return Resultado<Factura>.Fallo("Solo se pueden facturar servicios finalizados.");
                if (ReglasTaller.DatosServicio(servicio) is { } error) return Resultado<Factura>.Fallo(error);
                if (servicio.Vehiculo?.IdCliente != idCliente)
                    return Resultado<Factura>.Fallo("No se pueden mezclar clientes: todos los servicios deben pertenecer al cliente de la factura.");
            }
            if (await _db.DetalleFactura.AnyAsync(d => ids.Contains(d.IdServicio) && d.Factura!.Estado == ReglasTaller.Emitida))
                return Resultado<Factura>.Fallo("Este servicio ya tiene una factura vigente; no puede facturarse de nuevo.");
            // Servicio.Costo es el importe cobrado ya existente. No se suman precios actuales de productos como si fueran históricos.
            var subtotal = decimal.Round(servicios.Sum(s => s.Total), 2, MidpointRounding.ToEven);
            var iva = decimal.Round(subtotal * _config.PorcentajeIVA / 100m, 2, MidpointRounding.ToEven);
            var total = subtotal + iva;
            if (!ReglasTaller.Importe(subtotal) || !ReglasTaller.Importe(iva) || !ReglasTaller.Importe(total))
                return Resultado<Factura>.Fallo("Los importes de la factura exceden la precisión admitida por la base de datos.");
            var numero = await GenerarNumeroFactura();
            if (numero is null) return Resultado<Factura>.Fallo("La numeración de facturas necesita revisión; no se generó ni reutilizó ningún número.");
            var factura = new Factura { NumeroFactura = numero, IdCliente = idCliente, RUC = cliente.RFC,
                Subtotal = subtotal, IVA = iva, Total = total, MetodoPago = metodoPago, Estado = ReglasTaller.Emitida, Notas = notas };
            _db.Facturas.Add(factura);
            foreach (var servicio in servicios)
                _db.DetalleFactura.Add(new DetalleFactura { Factura = factura, IdServicio = servicio.IdServicio, Subtotal = servicio.Total });
            await _db.SaveChangesAsync();
            return Resultado<Factura>.Ok(factura, $"Factura {numero} creada correctamente.");
        }, Resultado<Factura>.Fallo);

    public Task<Resultado> AnularFactura(int idFactura, string? motivo) => OperacionTaller.Ejecutar(_db, async () =>
    {
        var factura = await _db.Facturas.SingleOrDefaultAsync(f => f.IdFactura == idFactura);
        if (factura is null) return Resultado.Fallo("La factura indicada no existe.");
        if (factura.Estado == ReglasTaller.Anulada) return Resultado.Fallo("La factura ya está anulada.");
        if (factura.Estado != ReglasTaller.Emitida) return Resultado.Fallo("La factura tiene un estado desconocido y necesita revisión.");
        motivo = ReglasTaller.Opcional(motivo) ?? "Anulación solicitada por el usuario";
        if (("Anulada: " + motivo).Length > 200) return Resultado.Fallo("El motivo de anulación no puede exceder 191 caracteres para guardarse completo en las notas.");
        if (_db.Database.IsSqlServer())
        {
            var filas = await _db.Database.ExecuteSqlRawAsync("EXEC dbo.usp_AnularFactura @IdFactura = {0}, @Motivo = {1}", idFactura, motivo);
            if (filas == 0) return Resultado.Fallo("No se pudo anular la factura.");
            await _db.Entry(factura).ReloadAsync();
        }
        else
        {
            factura.Estado = ReglasTaller.Anulada;
            factura.Notas = "Anulada: " + motivo;
            await _db.SaveChangesAsync();
        }
        // La anulación conserva documentos e importes y nunca repone Stock.
        return Resultado.Ok("Factura anulada correctamente.");
    }, Resultado.Fallo);

    public async Task<ResultadoPaginado<Factura>> Buscar(string? terminoNumero, int? idCliente, DateOnly? desde, DateOnly? hasta, int pagina, int tamPagina)
    {
        pagina = Math.Max(1, pagina);
        tamPagina = Math.Clamp(tamPagina, 5, 200);

        IQueryable<Factura> query = _db.Facturas.AsNoTracking().Include(f => f.Cliente);
        if (idCliente.HasValue)
            query = query.Where(f => f.IdCliente == idCliente.Value);
        if (!string.IsNullOrWhiteSpace(terminoNumero))
        {
            var t = terminoNumero.Trim();
            query = query.Where(f => f.NumeroFactura.Contains(t) || (f.RUC != null && f.RUC.Contains(t)) ||
                (f.Cliente != null && (f.Cliente.Nombre.Contains(t) || f.Cliente.Apellido.Contains(t) ||
                    (f.Cliente.RFC != null && f.Cliente.RFC.Contains(t)) ||
                    (f.Cliente.Cedula != null && f.Cliente.Cedula.Contains(t)))));
        }
        if (desde.HasValue)
        {
            var inicio = desde.Value.ToDateTime(TimeOnly.MinValue);
            query = query.Where(f => f.FechaFactura >= inicio);
        }
        if (hasta.HasValue)
        {
            var fin = hasta.Value.AddDays(1).ToDateTime(TimeOnly.MinValue);
            query = query.Where(f => f.FechaFactura < fin);
        }

        var total = await query.CountAsync();
        pagina = Paginacion.Ajustar(pagina, total, tamPagina);
        var elementos = await query
            .OrderByDescending(f => f.FechaFactura).ThenByDescending(f => f.IdFactura)
            .Skip((pagina - 1) * tamPagina).Take(tamPagina)
            .ToListAsync();

        return new ResultadoPaginado<Factura>(elementos, total, pagina, tamPagina);
    }

    public async Task<FacturaCompleta?> ObtenerParaImpresion(int idFactura)
    {
        var factura = await _db.Facturas.AsNoTracking()
            .Include(f => f.Cliente)
            .Include(f => f.DetalleFacturas).ThenInclude(df => df.Servicio).ThenInclude(s => s!.Vehiculo)
            .FirstOrDefaultAsync(f => f.IdFactura == idFactura);
        if (factura is null)
            return null;

        var detalles = factura.DetalleFacturas
            .OrderBy(df => df.IdServicio)
            .Select(df => new DetalleFacturaLinea(
                df.IdServicio,
                df.Servicio?.TipoServicio ?? "",
                df.Servicio?.Descripcion ?? "",
                df.Servicio?.Vehiculo?.Placa ?? "",
                df.Servicio?.FechaInicio ?? default,
                df.Subtotal))
            .ToList();

        return new FacturaCompleta(factura, detalles);
    }

    /// <summary>
    /// Servicios facturables del cliente: finalizados y no incluidos en una
    /// factura emitida (la anulación los libera para volver a facturarse).
    /// </summary>
    public async Task<List<ServicioElegible>> ServiciosElegibles(int idCliente)
    {
        var idsYaFacturados = _db.DetalleFactura
            .Where(df => df.Factura!.Estado == "Emitida")
            .Select(df => df.IdServicio);

        var servicios = await _db.Servicios.AsNoTracking()
            .Include(s => s.Vehiculo)
            .Where(s => s.Vehiculo!.IdCliente == idCliente &&
                        s.Estado == ReglasTaller.Finalizado &&
                        !idsYaFacturados.Contains(s.IdServicio))
            .OrderByDescending(s => s.FechaInicio)
            .ToListAsync();

        return servicios.Select(s => new ServicioElegible(
            s.IdServicio, s.IdVehiculo, s.Vehiculo?.Placa ?? "", s.TipoServicio,
            s.Descripcion, s.FechaInicio, s.Estado, s.Total)).ToList();
    }

    /// <summary>
    /// Número secuencial visible: FAC-{año}-{0001}. Generado dentro de la
    /// transacción protegida por el bloqueo compartido de SQL Server.
    /// </summary>
    private async Task<string?> GenerarNumeroFactura()
    {
        // Se ejecuta después del bloqueo transaccional compartido; incluye facturas anuladas.
        var prefijo = $"FAC-{DateTime.Now.Year}-";
        var numeros = await _db.Facturas.AsNoTracking().Where(f => f.NumeroFactura.StartsWith(prefijo))
            .Select(f => f.NumeroFactura).ToListAsync();
        long ultimo = 0;
        foreach (var numero in numeros)
        {
            if (!long.TryParse(numero.AsSpan(prefijo.Length), System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out var valor) || valor <= 0)
                return null;
            ultimo = Math.Max(ultimo, valor);
        }
        if (ultimo == long.MaxValue) return null;
        var siguiente = $"{prefijo}{ultimo + 1:0000}";
        return siguiente.Length <= 20 ? siguiente : null;
    }
}
