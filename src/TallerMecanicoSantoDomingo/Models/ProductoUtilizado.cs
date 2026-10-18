using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace TallerMecanicoSantoDomingo.Models;

/// <summary>
/// Entidad de relación UTILIZA del diagrama MER: REPUESTO con HISTORIAL DE SERVICIOS
/// (denominada Productos_Utilizados en la sección 4.7 de requisitos).
/// Regla 9: un servicio puede utilizar varios productos y un producto puede usarse
/// en distintos servicios; la pareja producto-servicio no se repite.
/// Regla 10: la cantidad debe ser mayor que cero.
/// </summary>
public class ProductoUtilizado
{
    [Key]
    public int IdProductoUtilizado { get; set; }

    public int IdServicio { get; set; }

    public int IdProducto { get; set; }

    /// <summary>Cantidad del producto utilizada en el servicio. Regla 10: > 0.</summary>
    [Precision(12, 3)]
    public decimal Cantidad { get; set; }

    public Servicio? Servicio { get; set; }
    public Producto? Producto { get; set; }
}
