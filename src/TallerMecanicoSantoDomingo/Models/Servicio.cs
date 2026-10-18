using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace TallerMecanicoSantoDomingo.Models;

/// <summary>Trabajo realizado a un vehículo.</summary>
public class Servicio
{
    [Key]
    public int IdServicio { get; set; }
    public int IdVehiculo { get; set; }

    [Required, MaxLength(20)]
    public string TipoServicio { get; set; } = "Mantenimiento";

    [Required, MaxLength(500)]
    public string Descripcion { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? DescripcionFalla { get; set; }

    [MaxLength(50)]
    public string? TipoPintura { get; set; }

    [MaxLength(50)]
    public string? Color { get; set; }

    [Required, MaxLength(20)]
    public string Estado { get; set; } = "Abierto";
    public DateOnly FechaInicio { get; set; }
    public DateOnly? FechaFin { get; set; }

    [Precision(12, 2)]
    public decimal Total { get; set; }
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    public Vehiculo? Vehiculo { get; set; }
    public ICollection<DetalleServicio> DetalleServicios { get; set; } = new List<DetalleServicio>();
    public ICollection<ProductoUtilizado> ProductosUtilizados { get; set; } = new List<ProductoUtilizado>();
    public ICollection<DetalleFactura> DetalleFacturas { get; set; } = new List<DetalleFactura>();
}
