using System.ComponentModel.DataAnnotations;

namespace TallerMecanicoSantoDomingo.Models;

/// <summary>Área en la que trabaja cada empleado.</summary>
public class AreaTrabajo
{
    [Key]
    public int IdArea { get; set; }

    [Required, MaxLength(50)]
    public string Nombre { get; set; } = string.Empty;

    public ICollection<Trabajador> Trabajadores { get; set; } = new List<Trabajador>();
}
