using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using TallerMecanicoSantoDomingo.Data;

namespace TallerMecanicoSantoDomingo.Services;

/// <summary>Una unidad de escritura y un bloqueo de aplicación de SQL Server compartido entre procesos.</summary>
internal static class OperacionTaller
{
    public static async Task<T> Ejecutar<T>(TallerDbContext db, Func<Task<T>> operacion, Func<string, T> fallo)
        where T : Resultado
    {
        // Cada escritura vuelve a leer los datos persistidos después de adquirir el bloqueo.
        db.ChangeTracker.Clear();
        await using var transaccion = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        try
        {
            if (db.Database.IsSqlServer())
            {
                // No crea objetos: el bloqueo transaccional reside en el gestor de bloqueos de SQL Server.
                await db.Database.ExecuteSqlRawAsync("""
                    DECLARE @resultado int;
                    EXEC @resultado = sys.sp_getapplock @Resource = N'Taller:operacion-negocio',
                        @LockMode = N'Exclusive', @LockOwner = N'Transaction', @LockTimeout = 15000;
                    IF @resultado < 0 THROW 51001, 'Operacion ocupada', 1;
                    """);
            }

            var resultado = await operacion();
            if (resultado.EsCorrecto)
                await transaccion.CommitAsync();
            else
            {
                await Revertir(transaccion);
                db.ChangeTracker.Clear();
            }
            return resultado;
        }
        catch (Exception ex) when (ex is DbUpdateException or SqlException or OverflowException)
        {
            await Revertir(transaccion);
            db.ChangeTracker.Clear();
            var sql = ex as SqlException ?? ex.InnerException as SqlException;
            return fallo(sql?.Number switch
            {
                2601 or 2627 => "El registro ya existe o fue registrado simultáneamente. Revise los datos e intente nuevamente.",
                1205 or 1222 or 51001 => "Otra operación está usando estos datos. No se guardaron cambios; vuelva a intentarlo.",
                547 => "La operación incumple una relación o validación de la base de datos. No se guardaron cambios.",
                515 => "Falta un dato requerido por la base de datos. No se guardaron cambios.",
                _ => "No se pudo confirmar la operación. Actualice la pantalla y revise los datos antes de volver a intentarlo."
            });
        }
    }

    private static async Task Revertir(IDbContextTransaction transaccion)
    {
        try { await transaccion.RollbackAsync(); }
        // SQL Server puede haber revertido la transacción antes de notificar un interbloqueo.
        // La operación sigue devolviendo fallo; nunca se transforma en un éxito.
        catch (InvalidOperationException) { }
        catch (SqlException ex) when (ex.Number is 3903 or 3971) { }
    }
}
