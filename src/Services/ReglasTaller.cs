using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.RegularExpressions;
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
    public const string PatronTelefono = "[0-9]{8}";
    public const string ErrorTelefono = "El teléfono debe contener exactamente 8 dígitos, sin espacios, guiones ni prefijos.";
    public const string ErrorNombre = "Use letras; se permiten tildes, espacios, guiones y apóstrofos entre palabras.";
    public const string ErrorContenido = "El campo debe contener letras o números; no se admiten solo signos ni caracteres de control.";
    public const string PatronIdentificadorVehiculo = " *(?:[A-Za-z0-9]+(?:[ -]+[A-Za-z0-9]+)*)? *";
    public const string ErrorIdentificadorVehiculo = "Use letras y números, separados únicamente por espacios o guiones.";
    public const string ErrorCorreo = "Ingrese un correo como nombre@dominio.com, sin espacios ni puntos consecutivos.";
    public const string ErrorImporte = "El importe debe admitir como máximo dos decimales.";
    public const string ErrorCantidad = "La cantidad debe admitir como máximo tres decimales.";
    public const string ErrorServiciosRepetidos = "Un mismo servicio no puede seleccionarse más de una vez en la factura.";
    public static string ErrorAnio => $"El año debe estar entre 1950 y {DateTime.Now.Year + 1}.";
    public static string? Opcional(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
    public static bool Importe(decimal valor) => valor >= 0 && valor <= MaxImporte && decimal.Round(valor, 2) == valor;
    public static bool Cantidad(decimal valor) => valor >= 0 && valor <= MaxCantidad && decimal.Round(valor, 3) == valor;
    public static bool TipoValido(string? tipo) => tipo is "Mantenimiento" or "Pintura";
    public static bool Compatible(string area, string tipo) =>
        (area == "Mantenimiento mecánico" && tipo == "Mantenimiento") || (area == "Pintura" && tipo == "Pintura");

    public static bool NombrePersonaValido(string? valor) => valor is not null && !valor.Trim().Any(char.IsControl) &&
        Regex.IsMatch(valor.Trim(), @"\A\p{L}[\p{L}\p{M}]*(?:(?: +|[-'’])\p{L}[\p{L}\p{M}]*)*\z", RegexOptions.NonBacktracking);

    public static bool ContenidoValido(string? valor) => string.IsNullOrWhiteSpace(valor) ||
        (valor.Any(char.IsLetterOrDigit) && !valor.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')));

    public static IEnumerable<ValidationResult> ValidarTextos(params (string Campo, string? Valor)[] campos)
    {
        foreach (var (campo, valor) in campos)
            if (!ContenidoValido(valor)) yield return new ValidationResult(ErrorContenido, [campo]);
    }

    public static bool IdentificadorVehiculoValido(string? valor) =>
        string.IsNullOrWhiteSpace(valor) || new RegularExpressionAttribute(PatronIdentificadorVehiculo).IsValid(valor);
    public static string ClaveVehiculo(string valor) => valor.Replace(" ", "").Replace("-", "").ToUpperInvariant();
    public static bool AnioVehiculoValido(int anio) => anio >= 1950 && anio <= DateTime.Now.Year + 1;

    public static bool CorreoValido(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return true;
        var correo = valor.Trim();
        if (correo.Length > 256 || correo.Any(char.IsWhiteSpace) || correo.Any(char.IsControl) ||
            correo.Contains("..") || !new EmailAddressAttribute().IsValid(correo)) return false;
        var partes = correo.Split('@');
        if (partes.Length != 2 || partes[0].StartsWith('.') || partes[0].EndsWith('.')) return false;
        var dominio = partes[1].Split('.');
        return dominio.Length >= 2 && dominio.All(p => p.Length is >= 1 and <= 63 &&
            char.IsAsciiLetterOrDigit(p[0]) && char.IsAsciiLetterOrDigit(p[^1]) &&
            p.All(c => char.IsAsciiLetterOrDigit(c) || c == '-'));
    }

    public static Task<bool> IdentificadorVehiculoRepetido(TallerDbContext db, string valor, bool chasis, int idVehiculo)
    {
        var clave = ClaveVehiculo(valor);
        var otros = db.Vehiculos.Where(v => v.IdVehiculo != idVehiculo);
        return chasis
            ? otros.AnyAsync(v => v.NumChasis != null && v.NumChasis.Replace(" ", "").Replace("-", "").ToUpper() == clave)
            : otros.AnyAsync(v => v.Placa.Replace(" ", "").Replace("-", "").ToUpper() == clave);
    }

    public static async Task<string?> ValidarTelefono(TallerDbContext db, string telefono,
        int? idCliente = null, int? idTrabajador = null)
    {
        if (telefono.Length != 8 || !telefono.All(char.IsAsciiDigit))
            return ErrorTelefono;

        // Compara también los formatos anteriores sin modificar las fichas guardadas.
        // Se ejecuta dentro del bloqueo de OperacionTaller, compartido por ambos módulos.
        var clientes = db.Clientes.Where(c => !idCliente.HasValue || c.IdCliente != idCliente.Value)
            .Select(c => c.Telefono.Trim().Replace(" ", "").Replace("-", "")
                .Replace("(", "").Replace(")", "").Replace(".", ""));
        var trabajadores = db.Trabajadores.Where(t => !idTrabajador.HasValue || t.IdTrabajador != idTrabajador.Value)
            .Select(t => t.Telefono.Trim().Replace(" ", "").Replace("-", "")
                .Replace("(", "").Replace(")", "").Replace(".", ""));
        if (await clientes.Concat(trabajadores).AnyAsync(t =>
            t == telefono || t == "+505" + telefono || t == "00505" + telefono))
            return "El teléfono ya está registrado para otro cliente o trabajador.";
        return null;
    }

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
            if ((entidad is Cliente or Trabajador) && (propiedad.Name is "Nombre" or "Apellido") && !NombrePersonaValido(valor))
                return $"{nombre}: {ErrorNombre}";
            if ((propiedad.Name is "Nombre" or "Direccion" or "Descripcion" or "DescripcionFalla" or "Marca" or "Modelo" or "Color" or "TipoPintura" or "Tipo") &&
                !ContenidoValido(valor)) return $"{nombre}: {ErrorContenido}";
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
