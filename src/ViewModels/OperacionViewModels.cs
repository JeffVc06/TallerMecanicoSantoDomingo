using System.ComponentModel.DataAnnotations;

namespace TallerMecanicoSantoDomingo.ViewModels;

public class ClienteFormViewModel
{
    public int IdCliente { get; set; }

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(100, ErrorMessage = "El nombre no puede exceder 100 caracteres.")]
    [Display(Name = "Nombre")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "El apellido es obligatorio.")]
    [StringLength(100, ErrorMessage = "El apellido no puede exceder 100 caracteres.")]
    [Display(Name = "Apellido")]
    public string Apellido { get; set; } = string.Empty;

    [Required(ErrorMessage = "El RFC es obligatorio.")]
    [StringLength(20, ErrorMessage = "El RFC no puede exceder 20 caracteres.")]
    [Display(Name = "RFC")]
    public string RFC { get; set; } = string.Empty;

    [Required(ErrorMessage = "El teléfono es obligatorio.")]
    [StringLength(20, ErrorMessage = "El teléfono no puede exceder 20 caracteres.")]
    [Phone(ErrorMessage = "Debe ingresar un teléfono válido.")]
    [Display(Name = "Teléfono")]
    public string Telefono { get; set; } = string.Empty;

    [Required(ErrorMessage = "La dirección es obligatoria.")]
    [StringLength(200, ErrorMessage = "La dirección no puede exceder 200 caracteres.")]
    [Display(Name = "Dirección")]
    public string Direccion { get; set; } = string.Empty;
}

public class VehiculoFormViewModel
{
    public int IdVehiculo { get; set; }

    [Required(ErrorMessage = "Debe seleccionar el cliente propietario.")]
    [Display(Name = "Cliente propietario")]
    public int IdCliente { get; set; }

    [Required(ErrorMessage = "La placa es obligatoria.")]
    [StringLength(20, ErrorMessage = "La placa no puede exceder 20 caracteres.")]
    [Display(Name = "Placa")]
    public string Placa { get; set; } = string.Empty;

    [Required(ErrorMessage = "La marca es obligatoria.")]
    [StringLength(50, ErrorMessage = "La marca no puede exceder 50 caracteres.")]
    [Display(Name = "Marca")]
    public string Marca { get; set; } = string.Empty;

    [Required(ErrorMessage = "El modelo es obligatorio.")]
    [StringLength(50, ErrorMessage = "El modelo no puede exceder 50 caracteres.")]
    [Display(Name = "Modelo")]
    public string Modelo { get; set; } = string.Empty;

    [Required(ErrorMessage = "El año es obligatorio.")]
    [Range(1950, 2100, ErrorMessage = "El año debe estar entre 1950 y 2100.")]
    [Display(Name = "Año")]
    public int Anio { get; set; } = DateTime.Now.Year;
}

public class TrabajadorFormViewModel
{
    public int IdTrabajador { get; set; }

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(100, ErrorMessage = "El nombre no puede exceder 100 caracteres.")]
    [Display(Name = "Nombre")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "El apellido es obligatorio.")]
    [StringLength(100, ErrorMessage = "El apellido no puede exceder 100 caracteres.")]
    [Display(Name = "Apellido")]
    public string Apellido { get; set; } = string.Empty;

    [Required(ErrorMessage = "El teléfono es obligatorio.")]
    [StringLength(20, ErrorMessage = "El teléfono no puede exceder 20 caracteres.")]
    [Phone(ErrorMessage = "Debe ingresar un teléfono válido.")]
    [Display(Name = "Teléfono")]
    public string Telefono { get; set; } = string.Empty;

    [Required(ErrorMessage = "Debe seleccionar el área de trabajo.")]
    [Display(Name = "Área de trabajo")]
    public string Area { get; set; } = string.Empty;
}

public class ServicioFormViewModel
{
    public int IdServicio { get; set; }

    [Required(ErrorMessage = "Debe seleccionar el vehículo.")]
    [Display(Name = "Vehículo")]
    public int IdVehiculo { get; set; }

    [Required(ErrorMessage = "Debe seleccionar el tipo de servicio.")]
    [Display(Name = "Tipo de servicio")]
    public string TipoServicio { get; set; } = "Mantenimiento";

    [Required(ErrorMessage = "La descripción del trabajo es obligatoria.")]
    [StringLength(500, ErrorMessage = "La descripción no puede exceder 500 caracteres.")]
    [Display(Name = "Descripción del trabajo")]
    public string Descripcion { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "La descripción de la falla no puede exceder 500 caracteres.")]
    [Display(Name = "Descripción de la falla (mantenimiento)")]
    public string? DescripcionFalla { get; set; }

    [StringLength(50, ErrorMessage = "El tipo de pintura no puede exceder 50 caracteres.")]
    [Display(Name = "Tipo de pintura")]
    public string? TipoPintura { get; set; }

    [StringLength(50, ErrorMessage = "El color no puede exceder 50 caracteres.")]
    [Display(Name = "Color")]
    public string? Color { get; set; }

    [Required(ErrorMessage = "La fecha de inicio es obligatoria.")]
    [Display(Name = "Fecha de inicio")]
    public DateOnly FechaInicio { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Range(typeof(decimal), "0", "99999999", ErrorMessage = "El monto del servicio debe ser mayor o igual a cero.")]
    [Display(Name = "Monto del servicio (C$)")]
    public decimal Total { get; set; }
}

public class FinalizarServicioViewModel
{
    public int IdServicio { get; set; }

    [Required(ErrorMessage = "La fecha de finalización es obligatoria.")]
    [Display(Name = "Fecha de finalización")]
    public DateOnly FechaFin { get; set; } = DateOnly.FromDateTime(DateTime.Today);
}

public class CancelarServicioViewModel
{
    public int IdServicio { get; set; }

    [StringLength(200, ErrorMessage = "El motivo no puede exceder 200 caracteres.")]
    [Display(Name = "Motivo de cancelación (opcional)")]
    public string? Motivo { get; set; }
}

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
    [Range(typeof(decimal), "0.001", "9999999", ErrorMessage = "La cantidad debe ser mayor que cero.")]
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
