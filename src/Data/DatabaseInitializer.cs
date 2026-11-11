using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace TallerMecanicoSantoDomingo.Data;

/// <summary>Prepara la base de datos y crea el administrador inicial si se configuró.</summary>
public static class DatabaseInitializer
{
    public static void Inicializar(IServiceProvider servicios)
    {
        using var scope = servicios.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TallerDbContext>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInitializer");
        var cadena = db.Database.GetConnectionString()
            ?? throw new InvalidOperationException("No se encontró la conexión 'TallerDB'.");
        var conexion = new SqlConnectionStringBuilder(cadena);
        if (conexion.InitialCatalog != "TallerMecanicoSantoDomingo")
            throw new InvalidOperationException("El script SQL requiere Database=TallerMecanicoSantoDomingo en la conexión.");

        using var recurso = typeof(TallerDbContext).Assembly.GetManifestResourceStream("EsquemaBD.sql")
            ?? throw new InvalidOperationException("No se encontró el esquema SQL incrustado.");
        using var lector = new StreamReader(recurso);
        var lotes = Regex.Split(lector.ReadToEnd(), @"(?im)^[ \t]*GO[ \t]*;?[ \t]*\r?$")
            .Where(lote => !string.IsNullOrWhiteSpace(lote))
            .ToArray();
        if (lotes is not [var crearBase, _, var crearTablas, var restricciones, var procedimiento])
            throw new InvalidOperationException("El script SQL no contiene los lotes esperados de creación y actualización.");

        conexion.InitialCatalog = "master";
        using var sql = new SqlConnection(conexion.ConnectionString);
        sql.Open();
        using var existe = new SqlCommand("SELECT DB_ID(N'TallerMecanicoSantoDomingo')", sql);
        if (existe.ExecuteScalar() is DBNull)
            EjecutarLote(sql, null, crearBase);
        sql.ChangeDatabase("TallerMecanicoSantoDomingo");

        using var transaccion = sql.BeginTransaction();
        var columnas = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        using (var consulta = new SqlCommand("""
            SELECT t.name, c.name
            FROM sys.tables AS t
            INNER JOIN sys.columns AS c ON c.object_id = t.object_id
            WHERE t.schema_id = SCHEMA_ID(N'dbo');
            """, sql, transaccion))
        using (var filas = consulta.ExecuteReader())
        {
            while (filas.Read())
            {
                var tabla = filas.GetString(0);
                if (!columnas.TryGetValue(tabla, out var nombres))
                    columnas[tabla] = nombres = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                nombres.Add(filas.GetString(1));
            }
        }

        if (NecesitaEsquema(db, columnas.Keys))
            EjecutarLote(sql, transaccion, crearTablas);
        else
            ValidarColumnas(db, columnas);

        try
        {
            EjecutarLote(sql, transaccion, restricciones);
        }
        catch (SqlException ex) when (ex.Number == 547)
        {
            throw new InvalidOperationException("Hay registros que incumplen una restricción del taller. Revise el detalle de SQL Server para corregirlos antes de iniciar.", ex);
        }

        using var existeProcedimiento = new SqlCommand("SELECT OBJECT_ID(N'dbo.usp_AnularFactura', N'P')", sql, transaccion);
        if (existeProcedimiento.ExecuteScalar() is DBNull)
            EjecutarLote(sql, transaccion, procedimiento);
        transaccion.Commit();

        var gestor = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        SemillarAdministradorInicial(db, gestor, config, logger).GetAwaiter().GetResult();
    }

    private static void EjecutarLote(SqlConnection conexion, SqlTransaction? transaccion, string lote)
    {
        using var comando = new SqlCommand(lote, conexion, transaccion);
        comando.ExecuteNonQuery();
    }

    private static void ValidarColumnas(TallerDbContext db, IReadOnlyDictionary<string, HashSet<string>> columnas)
    {
        var faltantes = db.Model.GetRelationalModel().Tables
            .SelectMany(t => t.Columns.Where(c => !columnas[t.Name].Contains(c.Name))
                .Select(c => $"{t.Name}.{c.Name}"))
            .ToArray();
        if (faltantes.Length > 0)
            throw new InvalidOperationException($"El esquema está incompleto. Faltan columnas: {string.Join(", ", faltantes)}.");
    }

    internal static bool NecesitaEsquema(TallerDbContext db, IEnumerable<string> tablas)
    {
        var existentes = tablas.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (existentes.Count == 0) return true;
        var esperadas = db.Model.GetEntityTypes().Select(e => e.GetTableName()!);
        if (esperadas.All(existentes.Contains)) return false;
        throw new InvalidOperationException("La base tiene un esquema anterior o incompleto. Prepare su migración antes de continuar; no se modificaron las tablas.");
    }

    /// <summary>
    /// Crea el primer administrador si no existe ningún usuario y se configuró
    /// "AdministradorInicial:Email" y "AdministradorInicial:Password"
    /// (recomendado: dotnet user-secrets). Si no se configura, el sistema ofrece
    /// la página de configuración inicial la primera vez.
    /// </summary>
    internal static async Task SemillarAdministradorInicial(
        TallerDbContext db,
        UserManager<IdentityUser> gestor,
        IConfiguration config,
        ILogger logger)
    {
        if (await db.Users.AnyAsync())
            return;

        var email = config["AdministradorInicial:Email"];
        var password = config["AdministradorInicial:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogInformation("No hay usuarios registrados. El sistema habilitará la página de configuración inicial para crear el primer administrador.");
            return;
        }

        var usuario = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
        var resultado = await gestor.CreateAsync(usuario, password);
        if (!resultado.Succeeded)
        {
            var errores = string.Join("; ", resultado.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"No se pudo crear el administrador inicial: {errores}");
        }

        logger.LogInformation("Primer administrador creado con el correo configurado.");
    }
}
