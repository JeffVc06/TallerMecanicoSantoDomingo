using System.ComponentModel.DataAnnotations;

namespace TallerMecanicoSantoDomingo.Models;

/// <summary>Empleado del taller y su área de trabajo.</summary>
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
    public int IdArea { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    public AreaTrabajo? Area { get; set; }
    public ICollection<DetalleServicio> DetalleServicios { get; set; } = new List<DetalleServicio>();

    public string NombreCompleto => $"{Nombre} {Apellido}";
}
