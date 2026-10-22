using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TallerMecanicoSantoDomingo.Models;

/// <summary>Vehículo registrado a nombre de un cliente.</summary>
[Table("Vehiculo")]
public class Vehiculo
{
    [Key]
    public int IdVehiculo { get; set; }
    public int IdCliente { get; set; }

    [Required, MaxLength(20)]
    public string Placa { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Marca { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Modelo { get; set; } = string.Empty;

    public int Anio { get; set; }
    public int Kilometraje { get; set; }

    [MaxLength(50)]
    public string? Color { get; set; }

    [MaxLength(50)]
    public string? NumChasis { get; set; }

    [MaxLength(50)]
    public string? Tipo { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    public Cliente? Cliente { get; set; }
    public ICollection<Servicio> Servicios { get; set; } = new List<Servicio>();
}
