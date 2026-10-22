using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TallerMecanicoSantoDomingo.Models;

/// <summary>Servicio incluido en una factura y su subtotal.</summary>
[Table("Detalle_Factura"), PrimaryKey(nameof(IdServicio), nameof(IdFactura))]
public class DetalleFactura
{
    public int IdFactura { get; set; }

    public int IdServicio { get; set; }

    [Precision(12, 2)]
    public decimal Subtotal { get; set; }

    public Factura? Factura { get; set; }
    public Servicio? Servicio { get; set; }
}
