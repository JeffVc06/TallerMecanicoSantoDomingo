using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TallerMecanicoSantoDomingo.Models;

/// <summary>Empleado del taller y su área de trabajo.</summary>
[Table("Trabajador")]
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
    // Se conserva la especialidad sin una tabla de áreas adicional.
    [Required, MaxLength(50)]
    public string Area { get; set; } = "Mantenimiento mecánico";

    [MaxLength(256)]
    public string? Correo { get; set; }

    public DateOnly? FechaSalida { get; set; }

    [Column("Estado")]
    public bool Activo { get; set; } = true;

    [Column("FechaIngreso")]
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    public ICollection<DetalleServicio> DetalleServicios { get; set; } = new List<DetalleServicio>();

    public string NombreCompleto => $"{Nombre} {Apellido}";
}
