using System.ComponentModel.DataAnnotations;

namespace TallerMecanicoSantoDomingo.Models;

/// <summary>Vehículo registrado a nombre de un cliente.</summary>
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
    public bool Activo { get; set; } = true;
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    public Cliente? Cliente { get; set; }
    public ICollection<Servicio> Servicios { get; set; } = new List<Servicio>();
}
