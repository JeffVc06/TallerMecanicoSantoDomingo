using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using TallerMecanicoSantoDomingo.Data;
using TallerMecanicoSantoDomingo.Models;

namespace TallerMecanicoSantoDomingo.Services;

internal static class ReglasTaller
{
    public const string Abierto = "Abierto", Finalizado = "Finalizado", Cancelado = "Cancelado";
    public const string Asignado = "Activo", Retirado = "Retirado", Emitida = "Emitida", Anulada = "Anulada";
    // Pendiente se contempla al contar datos previos; el esquema actual no permite crearlo.
    public static readonly string[] EstadosActivos = ["Abierto", "Pendiente"];
    public const decimal MaxImporte = 9999999999.99m, MaxCantidad = 999999999.999m;
    public static string? Opcional(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
    public static bool Importe(decimal valor) => valor >= 0 && valor <= MaxImporte && decimal.Round(valor, 2) == valor;
    public static bool Cantidad(decimal valor) => valor >= 0 && valor <= MaxCantidad && decimal.Round(valor, 3) == valor;
    public static bool TipoValido(string? tipo) => tipo is "Mantenimiento" or "Pintura";
    public static bool Compatible(string area, string tipo) =>
        (area == "Mantenimiento mecánico" && tipo == "Mantenimiento") || (area == "Pintura" && tipo == "Pintura");

    public static string? Campos(object entidad)
    {
        // Reutiliza las longitudes y los campos requeridos del modelo sin modificar sus mapeos.
        foreach (var propiedad in entidad.GetType().GetProperties().Where(p => p.PropertyType == typeof(string)))
        {
            var valor = propiedad.GetValue(entidad) as string;
            var nombre = propiedad.Name switch { "RFC" => "RUC", "Descripcion" => "descripción", "Telefono" => "teléfono", _ => propiedad.Name };
            if (propiedad.GetCustomAttribute<RequiredAttribute>() is not null && string.IsNullOrWhiteSpace(valor))
                return $"El campo {nombre} es obligatorio.";
            if (propiedad.GetCustomAttribute<MaxLengthAttribute>() is { } maximo && valor?.Length > maximo.Length)
                return $"El campo {nombre} no puede exceder {maximo.Length} caracteres.";
        }
        return null;
    }

    public static Task<bool> Facturado(TallerDbContext db, int idServicio) =>
        db.DetalleFactura.AnyAsync(d => d.IdServicio == idServicio && d.Factura!.Estado == Emitida);

    public static Task<int> Carga(TallerDbContext db, int idTrabajador) =>
        db.DetalleServicio.Where(d => d.IdTrabajador == idTrabajador && d.Estado == Asignado &&
            EstadosActivos.Contains(d.Servicio!.Estado)).Select(d => d.IdServicio).Distinct().CountAsync();

    public static async Task<string?> PuedeAsignar(TallerDbContext db, Trabajador? trabajador, string tipo)
    {
        if (trabajador is null) return "El trabajador indicado no existe.";
        if (!trabajador.Activo || trabajador.FechaSalida.HasValue)
            return "El trabajador está inactivo o tiene fecha de salida y no puede recibir trabajos.";
        if (!Compatible(trabajador.Area, tipo)) return "El trabajador pertenece a otra área.";
        if (await Carga(db, trabajador.IdTrabajador) >= 2)
            return "El trabajador ya tiene dos servicios activos; el máximo permitido es 2.";
        return null;
    }

    public static string? DatosServicio(Servicio servicio)
    {
        if (!TipoValido(servicio.TipoServicio)) return "El tipo de servicio debe ser Mantenimiento o Pintura.";
        if (Campos(servicio) is { } campos) return campos;
        if (!Importe(servicio.Total)) return "El monto del servicio debe ser no negativo, con máximo dos decimales y compatible con la base de datos.";
        if (servicio.FechaInicio == default) return "La fecha de inicio es obligatoria.";
        if (servicio.FechaFin < servicio.FechaInicio) return "La fecha de finalización no puede ser anterior a la fecha de inicio.";
        if (servicio.Estado == Finalizado && !servicio.FechaFin.HasValue) return "La fecha de finalización es obligatoria.";
        if (servicio.TipoServicio == "Mantenimiento" && string.IsNullOrWhiteSpace(servicio.DescripcionFalla))
            return "La descripción de la falla es obligatoria para mantenimiento.";
        if (servicio.TipoServicio == "Pintura" && (string.IsNullOrWhiteSpace(servicio.TipoPintura) || string.IsNullOrWhiteSpace(servicio.Color)))
            return "El tipo de pintura y el color son obligatorios para pintura.";
        return null;
    }
}
