using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace TallerMecanicoSantoDomingo.Models;

/// <summary>
/// Entidad SERVICIO del modelo relacional.
/// Unifica las entidades del MER HISTORIAL DE SERVICIOS (Id Historial, Fecha,
/// Tipo_Servicio, Descripción, Total), MANTENIMIENTO MECÁNICO (Descripción_Falla)
/// y TRABAJO DE PINTURA (Tipo_Pintura, Color, Descripción) en una sola tabla,
/// tal como lo exige la Regla 4 y la sección 4.3 del documento de requisitos.
/// Regla 3: un vehículo puede recibir muchos servicios; cada servicio corresponde
/// a un solo vehículo y los trabajos repetidos se conservan como registros independientes.
/// </summary>
public class Servicio
{
    [Key]
    public int IdServicio { get; set; }

    /// <summary>Relación TIENE: cada servicio corresponde a un solo vehículo (Regla 3).</summary>
    public int IdVehiculo { get; set; }

    /// <summary>MER: Tipo_Servicio. Valores: Mantenimiento | Pintura (Regla 4).</summary>
    [Required, MaxLength(20)]
    public string TipoServicio { get; set; } = "Mantenimiento";

    /// <summary>MER: Descripción del trabajo.</summary>
    [Required, MaxLength(500)]
    public string Descripcion { get; set; } = string.Empty;

    /// <summary>MER: MANTENIMIENTO MECÁNICO → Descripción_Falla (solo mantenimiento).</summary>
    [MaxLength(500)]
    public string? DescripcionFalla { get; set; }

    /// <summary>MER: TRABAJO DE PINTURA → Tipo_Pintura (solo pintura).</summary>
    [MaxLength(50)]
    public string? TipoPintura { get; set; }

    /// <summary>MER: TRABAJO DE PINTURA → Color (solo pintura).</summary>
    [MaxLength(50)]
    public string? Color { get; set; }

    /// <summary>Estado del servicio: Abierto | Finalizado | Cancelado (Reglas 4 y 8).</summary>
    [Required, MaxLength(20)]
    public string Estado { get; set; } = "Abierto";

    /// <summary>MER: Fecha / Fecha_Entrada (Regla 8).</summary>
    public DateOnly FechaInicio { get; set; }

    /// <summary>MER: fecha de finalización. Vacía mientras el trabajo está abierto (Regla 8).</summary>
    public DateOnly? FechaFin { get; set; }

    /// <summary>
    /// MER: Total del historial de servicios. Monto a cobrar por el servicio;
    /// se usa como subtotal al facturar (Regla 13).
    /// </summary>
    [Precision(12, 2)]
    public decimal Total { get; set; }

    /// <summary>Extensión técnica: auditoría de creación.</summary>
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    public Vehiculo? Vehiculo { get; set; }
    public ICollection<DetalleServicio> DetalleServicios { get; set; } = new List<DetalleServicio>();
    public ICollection<ProductoUtilizado> ProductosUtilizados { get; set; } = new List<ProductoUtilizado>();
    public ICollection<DetalleFactura> DetalleFacturas { get; set; } = new List<DetalleFactura>();
}
