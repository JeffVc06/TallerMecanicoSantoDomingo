using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace TallerMecanicoSantoDomingo.Data;

/// <summary>
/// Inicializador de la base de datos.
/// Estrategia de esquema: el archivo database/TallerMecanicoSantoDomingo.sql es la
/// fuente única del esquema (no se usan migraciones de EF Core). Este inicializador
/// aplica ese script automáticamente en el primer arranque si la base de datos no
/// existe todavía, y siembra el primer administrador cuando se configura.
/// </summary>
public static class DatabaseInitializer
{
    public static void Inicializar(IServiceProvider servicios)
    {
        using var scope = servicios.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TallerDbContext>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInitializer");

        var conexion = db.Database.GetConnectionString()
            ?? throw new InvalidOperationException("No se encontró la cadena de conexión 'TallerDB'.");

        var nombreBase = new SqlConnectionStringBuilder(conexion).InitialCatalog;
        if (string.IsNullOrWhiteSpace(nombreBase))
            throw new InvalidOperationException("La cadena de conexión 'TallerDB' no define la base de datos (Database=...).");

        CrearBaseDeDatosSiNoExiste(conexion, nombreBase, logger);

        if (!db.Database.CanConnect())
            throw new InvalidOperationException($"No se pudo conectar a la base de datos '{nombreBase}'. Verifique que SQL Server esté ejecutándose.");

        if (!ExistenTablasDelSistema(db))
        {
            logger.LogInformation("Aplicando el esquema de la base de datos desde database/TallerMecanicoSantoDomingo.sql ...");
            AplicarScriptEsquema(db, logger);
            logger.LogInformation("Esquema creado correctamente.");
        }

        SemillarAreas(db, logger);
        var gestorUsuarios = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        SemillarAdministradorInicial(db, gestorUsuarios, config, logger).GetAwaiter().GetResult();
    }

    private static void CrearBaseDeDatosSiNoExiste(string cadenaConexion, string nombreBase, ILogger logger)
    {
        var builder = new SqlConnectionStringBuilder(cadenaConexion) { InitialCatalog = "master" };
        using var conexion = new SqlConnection(builder.ConnectionString);
        conexion.Open();

        using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT DB_ID(@nombre)";
        comando.Parameters.AddWithValue("@nombre", nombreBase);
        var resultado = comando.ExecuteScalar();
        var existe = resultado is not null and not DBNull;

        if (!existe)
        {
            logger.LogInformation("La base de datos '{0}' no existe. Creándola...", nombreBase);
            using var crear = conexion.CreateCommand();
            // El nombre proviene de la configuración local, no de datos de usuario.
            crear.CommandText = $"CREATE DATABASE [{nombreBase.Replace("]", "]]")}]";
            crear.ExecuteNonQuery();
            logger.LogInformation("Base de datos creada.");
        }
    }

    private static bool ExistenTablasDelSistema(TallerDbContext db)
    {
        // No consultar DbSet aquí: en una base nueva las tablas aún no existen.
        // Verificar el conjunto esperado también permite completar un esquema parcial,
        // ya que el script usa comprobaciones IF OBJECT_ID antes de crear las tablas.
        const string sql = """
            SELECT CASE WHEN
                (SELECT COUNT(*)
                 FROM INFORMATION_SCHEMA.TABLES
                 WHERE TABLE_SCHEMA = 'dbo'
                 AND TABLE_NAME IN (
                       'AspNetRoles', 'AspNetUsers', 'AspNetRoleClaims', 'AspNetUserClaims',
                       'AspNetUserLogins', 'AspNetUserRoles', 'AspNetUserTokens',
                       'Clientes', 'Vehiculos', 'Areas', 'Trabajadores', 'Servicios',
                       'DetalleServicio', 'Productos', 'ProductosUtilizados', 'Facturas',
                       'DetalleFactura'
                   )) = 17
                AND OBJECT_ID('dbo.usp_AnularFactura', 'P') IS NOT NULL
                THEN 1 ELSE 0 END AS Value
            """;

        return db.Database.SqlQueryRaw<int>(sql).Single() == 1;
    }

    private static void AplicarScriptEsquema(TallerDbContext db, ILogger logger)
    {
        var recurso = typeof(TallerDbContext).Assembly.GetManifestResourceStream("EsquemaBD.sql")
            ?? throw new InvalidOperationException("No se encontró el recurso incrustado 'EsquemaBD.sql' con el esquema de la base de datos.");

        using var lector = new StreamReader(recurso);
        var script = lector.ReadToEnd();

        // El script usa sentencias GO (convención de sqlcmd/SSMS); se separan en lotes.
        var lotes = System.Text.RegularExpressions.Regex.Split(script, @"^\s*GO\s*;?\s*$",
            System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        foreach (var lote in lotes)
        {
            var texto = lote.Trim();
            if (texto.Length == 0 || EsSoloComentarios(texto))
                continue;

            db.Database.ExecuteSqlRaw(texto);
        }
    }

    private static bool EsSoloComentarios(string texto)
    {
        var sinLineas = System.Text.RegularExpressions.Regex.Replace(texto, @"--[^\r\n]*", string.Empty);
        return sinLineas.Trim().Length == 0;
    }

    /// <summary>
    /// Crea el primer administrador si no existe ningún usuario y se configuró
    /// "AdministradorInicial:Email" y "AdministradorInicial:Password"
    /// (recomendado: dotnet user-secrets). Si no se configura, el sistema ofrece
    /// la página de configuración inicial la primera vez.
    /// </summary>
    private static void SemillarAreas(TallerDbContext db, ILogger logger)
    {
        if (!db.Areas.Any())
        {
            db.Areas.AddRange(
                new Models.AreaTrabajo { Nombre = "Mantenimiento mecánico" },
                new Models.AreaTrabajo { Nombre = "Pintura" });
            db.SaveChanges();
            logger.LogInformation("Catálogo de áreas de trabajo inicializado.");
        }
    }
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
