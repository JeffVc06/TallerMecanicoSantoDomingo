using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace TallerMecanicoSantoDomingo.Models;

/// <summary>Factura emitida a un cliente por los servicios realizados.</summary>
public class Factura
{
    [Key]
    public int IdFactura { get; set; }

    [Required, MaxLength(20)]
    public string NumeroFactura { get; set; } = string.Empty;
    public int IdCliente { get; set; }
    public DateTime FechaFactura { get; set; } = DateTime.UtcNow;

    [Precision(12, 2)]
    public decimal Subtotal { get; set; }

    [Precision(12, 2)]
    public decimal IVA { get; set; }

    [Precision(12, 2)]
    public decimal Total { get; set; }

    [Required, MaxLength(30)]
    public string MetodoPago { get; set; } = "Efectivo";

    [Required, MaxLength(20)]
    public string Estado { get; set; } = "Emitida";

    [MaxLength(200)]
    public string? Notas { get; set; }

    public Cliente? Cliente { get; set; }
    public ICollection<DetalleFactura> DetalleFacturas { get; set; } = new List<DetalleFactura>();
}
