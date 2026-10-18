using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace TallerMecanicoSantoDomingo.Models;

/// <summary>Cantidad de un producto utilizada en un servicio.</summary>
public class ProductoUtilizado
{
    [Key]
    public int IdProductoUtilizado { get; set; }

    public int IdServicio { get; set; }

    public int IdProducto { get; set; }

    [Precision(12, 3)]
    public decimal Cantidad { get; set; }

    public Servicio? Servicio { get; set; }
    public Producto? Producto { get; set; }
}
