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

        // Consultar primero evita repetir los CREATE TABLE al reiniciar.
        conexion.InitialCatalog = "master";
        using var sql = new SqlConnection(conexion.ConnectionString);
        sql.Open();
        using var existe = new SqlCommand("SELECT DB_ID(N'TallerMecanicoSantoDomingo')", sql);
        var existeBase = existe.ExecuteScalar() is not DBNull;
        var tablas = new List<string>();
        if (existeBase)
        {
            sql.ChangeDatabase("TallerMecanicoSantoDomingo");
            using var consulta = new SqlCommand("SELECT name FROM sys.tables WHERE schema_id = SCHEMA_ID('dbo')", sql);
            using var lectorTablas = consulta.ExecuteReader();
            while (lectorTablas.Read()) tablas.Add(lectorTablas.GetString(0));
        }

        if (NecesitaEsquema(db, tablas))
        {
            using var recurso = typeof(TallerDbContext).Assembly.GetManifestResourceStream("EsquemaBD.sql")
                ?? throw new InvalidOperationException("No se encontró el esquema SQL incrustado.");
            using var lector = new StreamReader(recurso);
            var lotes = System.Text.RegularExpressions.Regex.Split(lector.ReadToEnd(), @"^\s*GO\s*;?\s*$",
                System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            // El primer bloque crea la base; se omite si ya existe y está vacía.
            foreach (var lote in lotes.Skip(existeBase ? 1 : 0))
            {
                if (string.IsNullOrWhiteSpace(lote)) continue;
                using var comando = new SqlCommand(lote, sql);
                comando.ExecuteNonQuery();
            }
        }

        var gestor = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        SemillarAdministradorInicial(db, gestor, config, logger).GetAwaiter().GetResult();
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
