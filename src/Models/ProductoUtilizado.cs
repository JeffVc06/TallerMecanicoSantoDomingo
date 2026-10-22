using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TallerMecanicoSantoDomingo.Models;

/// <summary>Cantidad de un producto utilizada en un servicio.</summary>
[Table("Productos_Utilizados"), PrimaryKey(nameof(IdProducto), nameof(IdServicio))]
public class ProductoUtilizado
{
    public int IdServicio { get; set; }

    public int IdProducto { get; set; }

    [Precision(12, 3)]
    public decimal Cantidad { get; set; }

    public Servicio? Servicio { get; set; }
    public Producto? Producto { get; set; }
}
