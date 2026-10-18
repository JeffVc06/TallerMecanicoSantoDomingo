using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace TallerMecanicoSantoDomingo.Models;

/// <summary>
/// Entidad DETALLE_FACTURA: relación entre una factura y los servicios facturados.
/// Regla 11: cada factura tiene al menos un detalle; cada detalle referencia una
/// factura y un servicio existentes; la pareja factura-servicio no se repite.
/// Regla 13: el Subtotal del detalle es el monto del servicio sin IVA.
/// Conserva la relación histórica de la factura con los servicios originales (Regla 14).
/// </summary>
public class DetalleFactura
{
    [Key]
    public int IdDetalleFactura { get; set; }

    public int IdFactura { get; set; }

    public int IdServicio { get; set; }

    /// <summary>Subtotal del servicio facturado, sin IVA (Regla 13).</summary>
    [Precision(12, 2)]
    public decimal Subtotal { get; set; }

    public Factura? Factura { get; set; }
    public Servicio? Servicio { get; set; }
}
