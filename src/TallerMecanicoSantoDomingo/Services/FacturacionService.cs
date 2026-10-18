using System.Data;
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
public interface IFacturacionService
{
    Task<Resultado<Factura>> CrearFactura(int idCliente, string metodoPago, List<int> idsServicios, string? notas);
    Task<Resultado> AnularFactura(int idFactura, string? motivo);
    Task<ResultadoPaginado<Factura>> Buscar(string? terminoNumero, int? idCliente, DateOnly? desde, DateOnly? hasta, int pagina, int tamPagina);
    Task<FacturaCompleta?> ObtenerParaImpresion(int idFactura);
    Task<List<ServicioElegible>> ServiciosElegibles(int idCliente);
}

public class FacturacionService : IFacturacionService
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
    public async Task<Resultado<Factura>> CrearFactura(int idCliente, string metodoPago, List<int> idsServicios, string? notas)
    {
        // Regla 11: una factura debe contener al menos un detalle.
        var ids = (idsServicios ?? new List<int>()).Distinct().ToList();
        if (ids.Count == 0)
            return Resultado<Factura>.Fallo("Debe seleccionar al menos un servicio para facturar (Regla 11).");

        if (!ValoresCatalogo.MetodosPago.Contains(metodoPago))
            return Resultado<Factura>.Fallo("El método de pago no es válido.");

        var cliente = await _db.Clientes.AsNoTracking().FirstOrDefaultAsync(c => c.IdCliente == idCliente);
        if (cliente is null)
            return Resultado<Factura>.Fallo("El cliente indicado no existe.");
        if (!cliente.Activo)
            return Resultado<Factura>.Fallo("El cliente está desactivado y no puede recibir facturas nuevas.");

        await using var transaccion = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        try
        {
            // Cargar los servicios con su vehículo y cliente propietario (Regla 12).
            var servicios = await _db.Servicios.AsNoTracking()
                .Include(s => s.Vehiculo)
                .Where(s => ids.Contains(s.IdServicio))
                .ToListAsync();

            if (servicios.Count != ids.Count)
                return Resultado<Factura>.Fallo("Uno o más servicios seleccionados no existen.");

            foreach (var servicio in servicios)
            {
                if (servicio.Estado == ValoresCatalogo.ServicioCancelado)
                    return Resultado<Factura>.Fallo($"El servicio #{servicio.IdServicio} está cancelado y no puede facturarse.");

                // Regla 12: todos los servicios deben pertenecer al mismo cliente,
                // obtenido a través del vehículo asociado al servicio.
                if (servicio.Vehiculo?.IdCliente != idCliente)
                    return Resultado<Factura>.Fallo($"El servicio #{servicio.IdServicio} pertenece a otro cliente. No se pueden mezclar clientes en la misma factura (Regla 12).");
            }

            // Un servicio no puede facturarse dos veces mientras la factura esté emitida.
            var yaFacturados = await _db.DetalleFactura.AsNoTracking()
                .Where(df => ids.Contains(df.IdServicio) && df.Factura!.Estado == ValoresCatalogo.FacturaEmitida)
                .Select(df => df.IdServicio)
                .ToListAsync();
            if (yaFacturados.Count > 0)
                return Resultado<Factura>.Fallo($"El servicio #{yaFacturados.First()} ya forma parte de una factura emitida (Regla 11: la pareja factura-servicio no se repite).");

            // Regla 13: cálculos exclusivamente en el servidor.
            var subtotal = Math.Round(servicios.Sum(s => s.Total), 2);
            var iva = Math.Round(subtotal * _config.PorcentajeIVA / 100m, 2);
            var total = subtotal + iva;

            var numero = await GenerarNumeroFactura();

            var factura = new Factura
            {
                NumeroFactura = numero,
                IdCliente = idCliente,
                FechaFactura = DateTime.UtcNow,
                Subtotal = subtotal,
                IVA = iva,
                Total = total,
                MetodoPago = metodoPago,
                Estado = ValoresCatalogo.FacturaEmitida,
                Notas = string.IsNullOrWhiteSpace(notas) ? null : notas.Trim()
            };
            _db.Facturas.Add(factura);

            foreach (var servicio in servicios)
            {
                _db.DetalleFactura.Add(new DetalleFactura
                {
                    Factura = factura,
                    IdServicio = servicio.IdServicio,
                    Subtotal = servicio.Total
                });
            }

            await _db.SaveChangesAsync();
            await transaccion.CommitAsync();
            return Resultado<Factura>.Ok(factura, $"Factura {numero} creada correctamente.");
        }
        catch
        {
            await transaccion.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Anulación controlada de una factura (Regla 14): no borra nada, marca la
    /// factura como anulada mediante el procedimiento almacenado usp_AnularFactura
    /// para preservar la trazabilidad histórica.
    /// </summary>
    public async Task<Resultado> AnularFactura(int idFactura, string? motivo)
    {
        var factura = await _db.Facturas.AsNoTracking().FirstOrDefaultAsync(f => f.IdFactura == idFactura);
        if (factura is null)
            return Resultado.Fallo("La factura indicada no existe.");
        if (factura.Estado == ValoresCatalogo.FacturaAnulada)
            return Resultado.Fallo("La factura ya está anulada.");

        if (_db.Database.IsSqlServer())
        {
            var filas = await _db.Database.ExecuteSqlRawAsync(
                "EXEC dbo.usp_AnularFactura @IdFactura = {0}, @Motivo = {1}",
                idFactura, motivo ?? "Anulación solicitada por el usuario");
            if (filas == 0)
                return Resultado.Fallo("No se pudo anular la factura.");
        }
        else
        {
            var existente = await _db.Facturas.FindAsync(idFactura);
            if (existente is null)
                return Resultado.Fallo("La factura indicada no existe.");
            existente.Estado = ValoresCatalogo.FacturaAnulada;
            if (!string.IsNullOrWhiteSpace(motivo))
                existente.Notas = $"Anulada: {motivo.Trim()}";
            await _db.SaveChangesAsync();
        }

        return Resultado.Ok("Factura anulada. Los servicios facturados quedan disponibles para una nueva factura.");
    }

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
            query = query.Where(f => f.NumeroFactura.Contains(t) ||
                (f.Cliente != null && (f.Cliente.Nombre.Contains(t) || f.Cliente.Apellido.Contains(t) || f.Cliente.RFC.Contains(t))));
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
        var elementos = await query
            .OrderByDescending(f => f.FechaFactura)
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
    /// Servicios facturables del cliente: no cancelados y no incluidos en una
    /// factura emitida (la anulación los libera para volver a facturarse).
    /// </summary>
    public async Task<List<ServicioElegible>> ServiciosElegibles(int idCliente)
    {
        var idsYaFacturados = _db.DetalleFactura
            .Where(df => df.Factura!.Estado == ValoresCatalogo.FacturaEmitida)
            .Select(df => df.IdServicio);

        var servicios = await _db.Servicios.AsNoTracking()
            .Include(s => s.Vehiculo)
            .Where(s => s.Vehiculo!.IdCliente == idCliente &&
                        s.Estado != ValoresCatalogo.ServicioCancelado &&
                        !idsYaFacturados.Contains(s.IdServicio))
            .OrderByDescending(s => s.FechaInicio)
            .ToListAsync();

        return servicios.Select(s => new ServicioElegible(
            s.IdServicio, s.IdVehiculo, s.Vehiculo?.Placa ?? "", s.TipoServicio,
            s.Descripcion, s.FechaInicio, s.Estado, s.Total)).ToList();
    }

    /// <summary>
    /// Número secuencial visible: FAC-{año}-{0001}. Generado dentro de la
    /// transacción; en SQL Server se bloquea la última fila del año con
    /// UPDLOCK/HOLDLOCK para evitar duplicados por concurrencia.
    /// </summary>
    private async Task<string> GenerarNumeroFactura()
    {
        var anio = DateTime.Now.Year;
        var prefijo = $"FAC-{anio}-";

        int siguiente;
        if (_db.Database.IsSqlServer())
        {
            var ultimo = await _db.Database.SqlQueryRaw<string>(
                "SELECT ISNULL(MAX(NumeroFactura), '') AS Value FROM Facturas WITH (UPDLOCK, HOLDLOCK) " +
                "WHERE NumeroFactura LIKE {0}",
                prefijo + "%").SingleAsync();

            siguiente = int.TryParse(ultimo[prefijo.Length..], out var n) ? n + 1 : 1;
        }
        else
        {
            var ultimo = await _db.Facturas.AsNoTracking()
                .Where(f => f.NumeroFactura.StartsWith(prefijo))
                .Select(f => f.NumeroFactura)
                .OrderByDescending(f => f)
                .FirstOrDefaultAsync();

            siguiente = ultimo != null && int.TryParse(ultimo[prefijo.Length..], out var n) ? n + 1 : 1;
        }

        return $"{prefijo}{siguiente:0000}";
    }
}
