using System.ComponentModel.DataAnnotations;

namespace TallerMecanicoSantoDomingo.Models;

/// <summary>
/// Entidad TRABAJADOR del diagrama MER.
/// Atributos del documento: Id Trabajador (PK), Nombre, Apellido, Teléfono,
/// Especialidad (Mecánico/Pintura → representada por IdArea, relación PERTENECE).
/// Regla 5: cada trabajador pertenece a una sola área a la vez.
/// Extensiones técnicas: Activo y FechaRegistro.
/// </summary>
public class Trabajador
{
    [Key]
    public int IdTrabajador { get; set; }

    [Required, MaxLength(100)]
    public string Nombre { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Apellido { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string Telefono { get; set; } = string.Empty;

    /// <summary>Área de trabajo (MER: PERTENECE, N:1). Regla 5.</summary>
    public int IdArea { get; set; }

    /// <summary>Extensión técnica: desactivación lógica (Regla 14).</summary>
    public bool Activo { get; set; } = true;

    /// <summary>Extensión técnica: auditoría de creación.</summary>
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    public AreaTrabajo? Area { get; set; }
    public ICollection<DetalleServicio> DetalleServicios { get; set; } = new List<DetalleServicio>();

    public string NombreCompleto => $"{Nombre} {Apellido}";
}
