using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace TallerMecanicoSantoDomingo.Data;

/// <summary>Comprueba la base existente sin DDL y crea el administrador inicial si se configuró.</summary>
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
        // La base SQL está terminada: este arranque solo lee su estructura.
        using var sql = new SqlConnection(cadena);
        sql.Open();
        var columnas = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        using (var consulta = new SqlCommand("""
            SELECT t.name, c.name
            FROM sys.tables AS t
            INNER JOIN sys.columns AS c ON c.object_id = t.object_id
            WHERE t.schema_id = SCHEMA_ID(N'dbo');
            """, sql))
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
            throw new InvalidOperationException("La base de datos no está preparada. Configure una base existente con el esquema del taller; el sistema no crea ni modifica su estructura.");
        ValidarColumnas(db, columnas);

        var gestor = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        SemillarAdministradorInicial(db, gestor, config, logger).GetAwaiter().GetResult();
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
        throw new InvalidOperationException("La base tiene un esquema anterior o incompleto. Configure la base terminada del taller; no se modificaron las tablas.");
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
