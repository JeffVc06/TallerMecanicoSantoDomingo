using System.ComponentModel.DataAnnotations;

namespace TallerMecanicoSantoDomingo.Models;

/// <summary>
/// Entidad VEHÍCULO del diagrama MER.
/// Atributos del documento: Id Vehículo (PK), Placa, Modelo, Marca, Año.
/// Relación POSEE: un cliente puede tener varios vehículos; cada vehículo pertenece a un único cliente (Regla 2).
/// Extensiones técnicas: IdCliente (FK), Activo y FechaRegistro.
/// </summary>
public class Vehiculo
{
    [Key]
    public int IdVehiculo { get; set; }

    /// <summary>Clave foránea hacia el cliente propietario (Regla 2).</summary>
    public int IdCliente { get; set; }

    [Required, MaxLength(20)]
    public string Placa { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Marca { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Modelo { get; set; } = string.Empty;

    public int Anio { get; set; }

    /// <summary>Extensión técnica: desactivación lógica (Regla 14).</summary>
    public bool Activo { get; set; } = true;

    /// <summary>Extensión técnica: auditoría de creación.</summary>
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    public Cliente? Cliente { get; set; }
    public ICollection<Servicio> Servicios { get; set; } = new List<Servicio>();
}
