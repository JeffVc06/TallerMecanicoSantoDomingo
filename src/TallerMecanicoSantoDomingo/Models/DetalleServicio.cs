using System.ComponentModel.DataAnnotations;

namespace TallerMecanicoSantoDomingo.Models;

/// <summary>
/// Entidad de relación ASIGNADO_A / TRABAJO del diagrama MER (denominada
/// Detalle_Servicio en el modelo relacional y en la sección 4.5 de requisitos).
/// Atributos del documento: Id Trabajo (PK → IdDetalleServicio),
/// Fecha_Asignación, Estado y Observaciones.
/// Reglas 5, 6 y 7: compatibilidad de área, no duplicar la pareja
/// servicio-trabajador y máximo dos servicios activos por trabajador.
/// </summary>
public class DetalleServicio
{
    [Key]
    public int IdDetalleServicio { get; set; }

    public int IdServicio { get; set; }

    public int IdTrabajador { get; set; }

    /// <summary>MER: Fecha_Asignación.</summary>
    public DateTime FechaAsignacion { get; set; } = DateTime.UtcNow;

    /// <summary>MER: Estado de la asignación. Valores: Activo | Retirado.</summary>
    [Required, MaxLength(20)]
    public string Estado { get; set; } = ValoresCatalogo.AsignacionActiva;

    /// <summary>MER: Observaciones de la asignación.</summary>
    [MaxLength(200)]
    public string? Observaciones { get; set; }

    public Servicio? Servicio { get; set; }
    public Trabajador? Trabajador { get; set; }
}
