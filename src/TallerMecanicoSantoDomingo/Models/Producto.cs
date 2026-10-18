using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace TallerMecanicoSantoDomingo.Models;

/// <summary>
/// Entidad REPUESTO del diagrama MER (denominada Producto en la sección 4.6 de requisitos).
/// Atributos del documento: Id Repuesto (PK), Nombre, Descripción, Precio_Unitario y Stock.
/// El Stock proviene del documento original; no se implementa un módulo de
/// inventario adicional (solo consulta y registro del dato).
/// Extensiones técnicas: Activo y FechaRegistro.
/// </summary>
public class Producto
{
    [Key]
    public int IdProducto { get; set; }

    [Required, MaxLength(100)]
    public string Nombre { get; set; } = string.Empty;

    [Required, MaxLength(300)]
    public string Descripcion { get; set; } = string.Empty;

    /// <summary>MER: Precio_Unitario. Regla 10: valores monetarios en decimal, >= 0.</summary>
    [Precision(12, 2)]
    public decimal PrecioUnitario { get; set; }

    /// <summary>MER: Stock. Regla 10: cantidades >= 0.</summary>
    [Precision(12, 3)]
    public decimal Stock { get; set; }

    /// <summary>Extensión técnica: desactivación lógica (Regla 14).</summary>
    public bool Activo { get; set; } = true;

    /// <summary>Extensión técnica: auditoría de creación.</summary>
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    public ICollection<ProductoUtilizado> ProductosUtilizados { get; set; } = new List<ProductoUtilizado>();
}
