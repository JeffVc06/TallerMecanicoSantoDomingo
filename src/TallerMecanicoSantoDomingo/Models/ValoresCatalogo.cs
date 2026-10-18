namespace TallerMecanicoSantoDomingo.Models;

/// <summary>
/// Valores permitidos para los catálogos de estado y tipo.
/// Coinciden con las restricciones CHECK definidas en el script SQL.
/// (Extensión técnica: los diagramas usan estos conceptos; aquí se formalizan como constantes).
/// </summary>
public static class ValoresCatalogo
{
    // Tipos de servicio (MER: Mantenimiento mecánico / Trabajo de pintura)
    public const string TipoMantenimiento = "Mantenimiento";
    public const string TipoPintura = "Pintura";

    // Estados del servicio (abiertos dejan de contar como activos al finalizarse o cancelarse)
    public const string ServicioAbierto = "Abierto";
    public const string ServicioFinalizado = "Finalizado";
    public const string ServicioCancelado = "Cancelado";

    // Estados de una asignación de trabajador (MER: ASIGNADO_A → Estado)
    public const string AsignacionActiva = "Activo";
    public const string AsignacionRetirada = "Retirado";

    // Estados de la factura (MER: FACTURA → Estado)
    public const string FacturaEmitida = "Emitida";
    public const string FacturaAnulada = "Anulada";

    // Métodos de pago (MER: FACTURA → Método_Pago)
    public const string PagoEfectivo = "Efectivo";
    public const string PagoTarjeta = "Tarjeta";
    public const string PagoTransferencia = "Transferencia";

    public static IReadOnlyList<string> TiposServicio { get; } = new[] { TipoMantenimiento, TipoPintura };
    public static IReadOnlyList<string> EstadosServicio { get; } = new[] { ServicioAbierto, ServicioFinalizado, ServicioCancelado };
    public static IReadOnlyList<string> EstadosFactura { get; } = new[] { FacturaEmitida, FacturaAnulada };
    public static IReadOnlyList<string> MetodosPago { get; } = new[] { PagoEfectivo, PagoTarjeta, PagoTransferencia };

    /// <summary>
    /// Regla de negocio 5: el área del trabajador debe ser compatible con el tipo de servicio.
    /// El área "Mantenimiento mecánico" corresponde al tipo "Mantenimiento"
    /// y el área "Pintura" corresponde al tipo "Pintura".
    /// </summary>
    public static bool EsAreaCompatibleConTipo(string nombreArea, string tipoServicio)
    {
        return (nombreArea, tipoServicio) switch
        {
            ("Mantenimiento mecánico", TipoMantenimiento) => true,
            ("Pintura", TipoPintura) => true,
            _ => false
        };
    }
}
