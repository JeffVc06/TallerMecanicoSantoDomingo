namespace TallerMecanicoSantoDomingo.Services;

/// <summary>
/// Configuración general del taller leída de appsettings.json (sección ConfiguracionTaller).
/// </summary>
public class ConfiguracionTaller
{
    public string NombreTaller { get; set; } = "Taller Mecánico Santo Domingo";
    public string DireccionTaller { get; set; } = "Managua, Nicaragua";
    public string TelefonoTaller { get; set; } = "0000-0000";

    /// <summary>
    /// Tasa de IVA en porcentaje. El documento original no define la tasa,
    /// por lo que se deja configurable. Valor inicial elegido: 15% (Nicaragua).
    /// </summary>
    public decimal PorcentajeIVA { get; set; } = 15m;
}

/// <summary>
/// Resultado uniforme de las operaciones de negocio.
/// </summary>
public class Resultado
{
    public bool EsCorrecto { get; init; }
    public string? Mensaje { get; init; }

    public static Resultado Ok(string? mensaje = null) => new() { EsCorrecto = true, Mensaje = mensaje };
    public static Resultado Fallo(string mensaje) => new() { EsCorrecto = false, Mensaje = mensaje };
}

/// <summary>Resultado de una operación que devuelve información al caller.</summary>
public class Resultado<T> : Resultado
{
    public T? Datos { get; init; }

    public static Resultado<T> Ok(T datos, string? mensaje = null) =>
        new() { EsCorrecto = true, Datos = datos, Mensaje = mensaje };

    public new static Resultado<T> Fallo(string mensaje) =>
        new() { EsCorrecto = false, Mensaje = mensaje };
}
