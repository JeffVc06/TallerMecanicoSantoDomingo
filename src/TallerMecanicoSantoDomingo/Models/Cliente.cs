using System.ComponentModel.DataAnnotations;

namespace TallerMecanicoSantoDomingo.Models;

/// <summary>
/// Entidad CLIENTE del diagrama MER.
/// Atributos del documento: Id Cliente (PK), Nombre, Apellido, Teléfono, Dirección, RFC.
/// Extensiones técnicas: Activo (desactivación lógica, Regla 14) y FechaRegistro (auditoría).
/// </summary>
public class Cliente
{
    [Key]
    public int IdCliente { get; set; }

    [Required, MaxLength(100)]
    public string Nombre { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Apellido { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string Telefono { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Direccion { get; set; } = string.Empty;

    /// <summary>RFC / identificación fiscal del cliente (atributo del MER).</summary>
    [Required, MaxLength(20)]
    public string RFC { get; set; } = string.Empty;

    /// <summary>Extensión técnica: desactivación lógica sin perder historial (Regla 14).</summary>
    public bool Activo { get; set; } = true;

    /// <summary>Extensión técnica: auditoría de creación.</summary>
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    public ICollection<Vehiculo> Vehiculos { get; set; } = new List<Vehiculo>();
    public ICollection<Factura> Facturas { get; set; } = new List<Factura>();

    public string NombreCompleto => $"{Nombre} {Apellido}";
}
