using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace TallerMecanicoSantoDomingo.ViewModels;

public class ClienteFormViewModel : IValidatableObject
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

    [StringLength(20, ErrorMessage = "El RUC no puede exceder 20 caracteres.")]
    [Display(Name = "RUC de la empresa (opcional)")]
    public string? RFC { get; set; }

    [StringLength(20, ErrorMessage = "El campo {0} no puede exceder {1} caracteres.")]
    [Display(Name = "Cédula")]
    public string? Cedula { get; set; }

    [Required(ErrorMessage = "El teléfono es obligatorio.")]
    [StringLength(20, ErrorMessage = "El teléfono no puede exceder 20 caracteres.")]
    [Phone(ErrorMessage = "Debe ingresar un teléfono válido.")]
    [Display(Name = "Teléfono")]
    public string Telefono { get; set; } = string.Empty;

    [Required(ErrorMessage = "La dirección es obligatoria.")]
    [StringLength(200, ErrorMessage = "La dirección no puede exceder 200 caracteres.")]
    [Display(Name = "Dirección")]
    public string Direccion { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Cedula) && string.IsNullOrWhiteSpace(RFC))
            yield return new ValidationResult("Ingrese la cédula del cliente o el RUC de la empresa.", [nameof(Cedula), nameof(RFC)]);
    }
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
    [BindRequired]
    public int Anio { get; set; } = DateTime.Now.Year;

    [Range(0, int.MaxValue, ErrorMessage = "El kilometraje debe ser mayor o igual a cero.")]
    public int Kilometraje { get; set; }

    [StringLength(50, ErrorMessage = "El campo {0} no puede exceder {1} caracteres.")]
    public string? Color { get; set; }

    [StringLength(50, ErrorMessage = "El campo {0} no puede exceder {1} caracteres.")]
    [Display(Name = "Número de chasis")]
    public string? NumChasis { get; set; }

    [StringLength(50, ErrorMessage = "El campo {0} no puede exceder {1} caracteres.")]
    [Display(Name = "Tipo de vehículo")]
    public string? Tipo { get; set; }
}

public class TrabajadorFormViewModel : IValidatableObject
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

    [StringLength(256, ErrorMessage = "El campo {0} no puede exceder {1} caracteres."), EmailAddress(ErrorMessage = "Debe ingresar un correo válido.")]
    public string? Correo { get; set; }

    [Required(ErrorMessage = "La fecha de ingreso es obligatoria.")]
    [Display(Name = "Fecha de ingreso")]
    [BindRequired]
    public DateOnly? FechaIngreso { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    [Display(Name = "Fecha de salida")]
    public DateOnly? FechaSalida { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (FechaSalida < FechaIngreso)
            yield return new ValidationResult("La fecha de salida debe ser igual o posterior a la de ingreso.", [nameof(FechaSalida)]);
    }
}

public class ServicioFormViewModel : IValidatableObject
{
    public int IdServicio { get; set; }

    [Required(ErrorMessage = "Debe seleccionar el vehículo.")]
    [Display(Name = "Vehículo")]
    public int IdVehiculo { get; set; }

    [Required(ErrorMessage = "Debe seleccionar el tipo de servicio.")]
    [Display(Name = "Tipo de servicio")]
    [BindRequired]
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
    [BindRequired]
    public DateOnly FechaInicio { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Range(typeof(decimal), "0", "9999999999.99", ErrorMessage = "El monto debe ser no negativo y estar dentro del rango de la base de datos.")]
    [Display(Name = "Monto del servicio (C$)")]
    [BindRequired]
    public decimal Total { get; set; }
    [Display(Name = "Trabajador inicial")]
    public int? IdTrabajadorInicial { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (IdServicio == 0 && (!IdTrabajadorInicial.HasValue || IdTrabajadorInicial <= 0))
            yield return new ValidationResult("Seleccione un trabajador habilitado para abrir el servicio.", [nameof(IdTrabajadorInicial)]);
        if (TipoServicio == "Mantenimiento" && string.IsNullOrWhiteSpace(DescripcionFalla))
            yield return new ValidationResult("La descripción de la falla es obligatoria para mantenimiento.", [nameof(DescripcionFalla)]);
        if (TipoServicio == "Pintura")
        {
            if (string.IsNullOrWhiteSpace(TipoPintura)) yield return new ValidationResult("El tipo de pintura es obligatorio.", [nameof(TipoPintura)]);
            if (string.IsNullOrWhiteSpace(Color)) yield return new ValidationResult("El color es obligatorio para pintura.", [nameof(Color)]);
        }
    }
}

public class FinalizarServicioViewModel
{
    public int IdServicio { get; set; }

    [Required(ErrorMessage = "La fecha de finalización es obligatoria.")]
    [Display(Name = "Fecha de finalización")]
    [BindRequired]
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

    // Comparación contra el saldo persistido para detectar un formulario de edición desactualizado.
    public decimal? StockOriginal { get; set; }

    [Required(ErrorMessage = "El nombre del producto es obligatorio.")]
    [StringLength(100, ErrorMessage = "El nombre no puede exceder 100 caracteres.")]
    [Display(Name = "Nombre del producto")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "La descripción es obligatoria.")]
    [StringLength(300, ErrorMessage = "La descripción no puede exceder 300 caracteres.")]
    [Display(Name = "Descripción")]
    public string Descripcion { get; set; } = string.Empty;

    [Required(ErrorMessage = "El precio unitario es obligatorio.")]
    [Range(typeof(decimal), "0", "9999999999.99", ErrorMessage = "El precio debe ser no negativo y estar dentro del rango de la base de datos.")]
    [Display(Name = "Precio unitario (C$)")]
    [BindRequired]
    public decimal PrecioUnitario { get; set; }

    [Range(typeof(decimal), "0", "9999999999.99", ErrorMessage = "El costo de compra debe estar entre 0 y 9 999 999 999,99.")]
    [Display(Name = "Costo de compra (C$)")]
    public decimal Costo { get; set; }

    [Required(ErrorMessage = "El stock es obligatorio.")]
    [Range(typeof(decimal), "0", "999999999.999", ErrorMessage = "El stock debe ser no negativo y estar dentro del rango de la base de datos.")]
    [Display(Name = "Stock")]
    [BindRequired]
    public decimal Stock { get; set; }
}

public class ProductoUtilizadoFormViewModel
{
    public int IdServicio { get; set; }

    [Required(ErrorMessage = "Debe seleccionar el producto.")]
    [Display(Name = "Producto")]
    public int IdProducto { get; set; }

    [Required(ErrorMessage = "La cantidad es obligatoria.")]
    [Range(typeof(decimal), "0.001", "999999999.999", ErrorMessage = "La cantidad debe ser mayor que cero.")]
    [Display(Name = "Cantidad utilizada")]
    [BindRequired]
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
    [BindRequired]
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
