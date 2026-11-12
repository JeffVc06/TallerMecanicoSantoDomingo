using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using TallerMecanicoSantoDomingo.Data;
using TallerMecanicoSantoDomingo.Models;

namespace TallerMecanicoSantoDomingo.Services;

public class TrabajadorService(TallerDbContext db)
{
    public Task<Resultado> Guardar(Trabajador entrada, bool nuevo) => OperacionTaller.Ejecutar(db, async () =>
    {
        entrada.Nombre = entrada.Nombre?.Trim() ?? "";
        entrada.Apellido = entrada.Apellido?.Trim() ?? "";
        entrada.Telefono = entrada.Telefono?.Trim() ?? "";
        entrada.Area = entrada.Area?.Trim() ?? "";
        entrada.Correo = ReglasTaller.Opcional(entrada.Correo)?.ToLowerInvariant();
        if (ReglasTaller.Campos(entrada) is { } error) return Resultado.Fallo(error);
        if (entrada.Area is not ("Mantenimiento mecánico" or "Pintura")) return Resultado.Fallo("El área de trabajo seleccionada no existe.");
        if (!new PhoneAttribute().IsValid(entrada.Telefono)) return Resultado.Fallo("Debe ingresar un teléfono válido.");
        if (entrada.Correo is not null && !new EmailAddressAttribute().IsValid(entrada.Correo)) return Resultado.Fallo("Debe ingresar un correo válido.");
        if (entrada.FechaRegistro == default || entrada.FechaSalida < DateOnly.FromDateTime(entrada.FechaRegistro))
            return Resultado.Fallo("La fecha de ingreso es obligatoria y la salida no puede ser anterior al ingreso.");
        if (entrada.Correo is not null && await db.Trabajadores.AnyAsync(t => t.IdTrabajador != entrada.IdTrabajador &&
            t.Correo != null && t.Correo.ToLower() == entrada.Correo))
            return Resultado.Fallo("El correo ya está registrado para otro trabajador.");
        if (nuevo)
        {
            if (entrada.IdTrabajador != 0) return Resultado.Fallo("El identificador del trabajador se genera automáticamente.");
            entrada.DetalleServicios = [];
            db.Trabajadores.Add(entrada);
        }
        else
        {
            var existente = await db.Trabajadores.SingleOrDefaultAsync(t => t.IdTrabajador == entrada.IdTrabajador);
            if (existente is null) return Resultado.Fallo("El trabajador indicado no existe.");
            if ((existente.Area != entrada.Area || entrada.FechaSalida.HasValue) && await ReglasTaller.Carga(db, existente.IdTrabajador) > 0)
                return Resultado.Fallo("Resuelva las asignaciones activas antes de cambiar el área o registrar la salida del trabajador.");
            existente.Nombre = entrada.Nombre;
            existente.Apellido = entrada.Apellido;
            existente.Telefono = entrada.Telefono;
            existente.Area = entrada.Area;
            existente.Correo = entrada.Correo;
            existente.FechaRegistro = entrada.FechaRegistro;
            existente.FechaSalida = entrada.FechaSalida;
        }
        await db.SaveChangesAsync();
        return Resultado.Ok(nuevo ? "Trabajador registrado correctamente." : "Trabajador actualizado correctamente.");
    }, Resultado.Fallo);

    public Task<Resultado> CambiarEstado(int id, bool activo) => OperacionTaller.Ejecutar(db, async () =>
    {
        var trabajador = await db.Trabajadores.SingleOrDefaultAsync(t => t.IdTrabajador == id);
        if (trabajador is null) return Resultado.Fallo("El trabajador indicado no existe.");
        if (!activo && await ReglasTaller.Carga(db, id) > 0)
            return Resultado.Fallo("Resuelva las asignaciones activas antes de desactivar al trabajador.");
        if (activo && trabajador.FechaSalida.HasValue)
            return Resultado.Fallo("Quite la fecha de salida antes de reactivar al trabajador.");
        if (activo)
        {
            if (trabajador.Area is not ("Mantenimiento mecánico" or "Pintura"))
                return Resultado.Fallo("Revise el área del trabajador antes de reactivarlo.");
            if (await ReglasTaller.Carga(db, id) > 2)
                return Resultado.Fallo("El trabajador supera dos servicios activos. Resuelva sus asignaciones antes de reactivarlo.");
            var tipo = trabajador.Area == "Pintura" ? "Pintura" : "Mantenimiento";
            if (await db.DetalleServicio.AnyAsync(d => d.IdTrabajador == id && d.Estado == ReglasTaller.Asignado &&
                ReglasTaller.EstadosActivos.Contains(d.Servicio!.Estado) && d.Servicio.TipoServicio != tipo))
                return Resultado.Fallo("El trabajador tiene asignaciones activas de otra área. Resuélvalas antes de reactivarlo.");
        }
        trabajador.Activo = activo;
        await db.SaveChangesAsync();
        return Resultado.Ok(activo ? "Trabajador reactivado correctamente." : "Trabajador desactivado.");
    }, Resultado.Fallo);
}
