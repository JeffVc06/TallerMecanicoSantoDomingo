using System.ComponentModel.DataAnnotations;

namespace TallerMecanicoSantoDomingo.Models;

/// <summary>
/// Entidad ÁREA DE TRABAJO del diagrama MER.
/// El MER define dos áreas: Mantenimiento mecánico y Pintura.
/// El atributo "Especialidad (Mecánico/Pintura)" de TRABAJADOR queda representado
/// por esta relación N:1 (PERTENECE), evitando redundancia (3FN).
/// </summary>
public class AreaTrabajo
{
    [Key]
    public int IdArea { get; set; }

    [Required, MaxLength(50)]
    public string Nombre { get; set; } = string.Empty;

    public ICollection<Trabajador> Trabajadores { get; set; } = new List<Trabajador>();
}
