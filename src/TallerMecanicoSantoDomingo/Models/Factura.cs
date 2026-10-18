using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace TallerMecanicoSantoDomingo.Models;

/// <summary>
/// Entidad FACTURA del diagrama MER.
/// Atributos del documento: Id Factura (PK), Fecha_Factura, Total, Método_Pago y Estado.
/// Extensiones técnicas necesarias para las reglas 11 a 13: NumeroFactura (identificador
/// visible y único), IdCliente (FK de la relación GENERA), Subtotal e IVA (Regla 13
/// exige registrar el importe del IVA). Los subtotales NO incluyen IVA;
/// Total = Subtotal + IVA (también verificado por restricción CHECK en SQL Server).
/// Regla 14: las facturas emitidas no se eliminan físicamente, solo se anulan.
/// </summary>
public class Factura
{
    [Key]
    public int IdFactura { get; set; }

    /// <summary>Extensión técnica: número visible único, por ejemplo FAC-2026-0001.</summary>
    [Required, MaxLength(20)]
    public string NumeroFactura { get; set; } = string.Empty;

    /// <summary>Relación GENERA: la factura pertenece a un cliente (Regla 12).</summary>
    public int IdCliente { get; set; }

    /// <summary>MER: Fecha_Factura.</summary>
    public DateTime FechaFactura { get; set; } = DateTime.UtcNow;

    /// <summary>Extensión técnica: suma de los subtotales de los detalles, sin IVA (Regla 13).</summary>
    [Precision(12, 2)]
    public decimal Subtotal { get; set; }

    /// <summary>Extensión técnica: importe del IVA cobrado (Regla 13).</summary>
    [Precision(12, 2)]
    public decimal IVA { get; set; }

    /// <summary>MER: Total. Subtotal + IVA (Regla 13).</summary>
    [Precision(12, 2)]
    public decimal Total { get; set; }

    /// <summary>MER: Método_Pago (dato registrado; el sistema no controla pagos).</summary>
    [Required, MaxLength(30)]
    public string MetodoPago { get; set; } = "Efectivo";

    /// <summary>MER: Estado. Valores: Emitida | Anulada.</summary>
    [Required, MaxLength(20)]
    public string Estado { get; set; } = "Emitida";

    [MaxLength(200)]
    public string? Notas { get; set; }

    public Cliente? Cliente { get; set; }
    public ICollection<DetalleFactura> DetalleFacturas { get; set; } = new List<DetalleFactura>();
}
