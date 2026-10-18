using System.ComponentModel.DataAnnotations;

namespace TallerMecanicoSantoDomingo.ViewModels;

public class ProductoFormViewModel
{
    public int IdProducto { get; set; }

    [Required(ErrorMessage = "El nombre del producto es obligatorio.")]
    [StringLength(100, ErrorMessage = "El nombre no puede exceder 100 caracteres.")]
    [Display(Name = "Nombre del producto")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "La descripción es obligatoria.")]
    [StringLength(300, ErrorMessage = "La descripción no puede exceder 300 caracteres.")]
    [Display(Name = "Descripción")]
    public string Descripcion { get; set; } = string.Empty;

    [Required(ErrorMessage = "El precio unitario es obligatorio.")]
    [Range(typeof(decimal), "0", "99999999", ErrorMessage = "El precio unitario debe ser mayor o igual a cero.")]
    [Display(Name = "Precio unitario (C$)")]
    public decimal PrecioUnitario { get; set; }

    [Required(ErrorMessage = "El stock es obligatorio.")]
    [Range(typeof(decimal), "0", "9999999", ErrorMessage = "El stock no puede ser negativo.")]
    [Display(Name = "Stock")]
    public decimal Stock { get; set; }
}

public class ProductoUtilizadoFormViewModel
{
    public int IdServicio { get; set; }

    [Required(ErrorMessage = "Debe seleccionar el producto.")]
    [Display(Name = "Producto")]
    public int IdProducto { get; set; }

    [Required(ErrorMessage = "La cantidad es obligatoria.")]
    [Range(typeof(decimal), "0.001", "9999999", ErrorMessage = "La cantidad debe ser mayor que cero (Regla 10).")]
    [Display(Name = "Cantidad utilizada")]
    public decimal Cantidad { get; set; } = 1;
}

public class AsignarTrabajadorViewModel
{
    public int IdServicio { get; set; }

    [Required(ErrorMessage = "Debe seleccionar el trabajador.")]
    [Display(Name = "Trabajador")]
    public int IdTrabajador { get; set; }

    [StringLength(200, ErrorMessage = "Las observaciones no pueden exceder 200 caracteres.")]
    [Display(Name = "Observaciones (opcional)")]
    public string? Observaciones { get; set; }
}

public class FacturaCrearViewModel
{
    [Required(ErrorMessage = "Debe seleccionar el cliente.")]
    [Display(Name = "Cliente")]
    public int IdCliente { get; set; }

    [Required(ErrorMessage = "Debe seleccionar el método de pago.")]
    [Display(Name = "Método de pago")]
    public string MetodoPago { get; set; } = "Efectivo";

    [Required(ErrorMessage = "Debe seleccionar al menos un servicio para facturar.")]
    [Display(Name = "Servicios a facturar")]
    public List<int> IdServicios { get; set; } = new();

    [StringLength(200, ErrorMessage = "Las notas no pueden exceder 200 caracteres.")]
    [Display(Name = "Notas (opcional)")]
    public string? Notas { get; set; }

    /// <summary>Porcentaje de IVA mostrado en el formulario (solo informativo; el cálculo es del servidor).</summary>
    public decimal PorcentajeIVA { get; set; }
}

public class BusquedaFechaViewModel
{
    [Display(Name = "Desde")]
    public DateOnly? Desde { get; set; }

    [Display(Name = "Hasta")]
    public DateOnly? Hasta { get; set; }

    [Display(Name = "Cliente")]
    public int? IdCliente { get; set; }

    [Display(Name = "Trabajador")]
    public int? IdTrabajador { get; set; }

    [Display(Name = "Tipo de servicio")]
    public string? TipoServicio { get; set; }

    [Display(Name = "Estado")]
    public string? Estado { get; set; }
}
