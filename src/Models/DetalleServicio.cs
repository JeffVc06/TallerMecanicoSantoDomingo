using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TallerMecanicoSantoDomingo.Models;

/// <summary>Asignación de un trabajador a un servicio.</summary>
[Table("Detalle_Servicio"), PrimaryKey(nameof(IdServicio), nameof(IdTrabajador))]
public class DetalleServicio
{
    public int IdServicio { get; set; }

    public int IdTrabajador { get; set; }
    public DateTime FechaAsignacion { get; set; } = DateTime.UtcNow;

    [Required, MaxLength(20)]
    public string Estado { get; set; } = "Activo";

    [MaxLength(200)]
    public string? Observaciones { get; set; }

    public Servicio? Servicio { get; set; }
    public Trabajador? Trabajador { get; set; }
}
