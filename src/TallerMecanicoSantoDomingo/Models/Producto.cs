using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace TallerMecanicoSantoDomingo.Models;

/// <summary>Producto o repuesto disponible para los servicios.</summary>
public class Producto
{
    [Key]
    public int IdProducto { get; set; }

    [Required, MaxLength(100)]
    public string Nombre { get; set; } = string.Empty;

    [Required, MaxLength(300)]
    public string Descripcion { get; set; } = string.Empty;

    [Precision(12, 2)]
    public decimal PrecioUnitario { get; set; }

    [Precision(12, 3)]
    public decimal Stock { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    public ICollection<ProductoUtilizado> ProductosUtilizados { get; set; } = new List<ProductoUtilizado>();
}
