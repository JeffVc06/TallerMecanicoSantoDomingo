using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TallerMecanicoSantoDomingo.Data;
using TallerMecanicoSantoDomingo.Services;
using Xunit;

namespace TallerMecanicoSantoDomingo.Tests;

/// <summary>Comprueba la traducción real de EF a SQL Server sin abrir una conexión.</summary>
public class PruebasTraduccionSql
{
    [Fact]
    public async Task Placas_y_chasis_se_comparan_sin_separadores_y_sin_distinguir_mayusculas_en_sql()
    {
        var comandos = new ComandosVacios();
        using var db = new TallerDbContext(new DbContextOptionsBuilder<TallerDbContext>()
            .UseSqlServer("Server=unused;Database=unused;User Id=unused;Password=unused")
            .AddInterceptors(new SinConexion(), comandos).Options);

        Assert.False(await ReglasTaller.IdentificadorVehiculoRepetido(db, " mga-1234 ", chasis: false, 1));
        Assert.False(await ReglasTaller.IdentificadorVehiculoRepetido(db, " abcd-123 ", chasis: true, 1));
        Assert.Equal(2, comandos.Sql.Count);
        Assert.All(comandos.Sql, sql =>
        {
            Assert.Contains("[dbo].[Vehiculo]", sql);
            Assert.Contains("REPLACE", sql);
            Assert.Contains("UPPER", sql);
        });
    }

    [Fact]
    public async Task Telefonos_se_comparan_en_ambos_modulos_con_formato_previo_y_exclusion_de_la_ficha_propia_en_sql()
    {
        var comandos = new ComandosVacios();
        using var db = new TallerDbContext(new DbContextOptionsBuilder<TallerDbContext>()
            .UseSqlServer("Server=unused;Database=unused;User Id=unused;Password=unused")
            .AddInterceptors(new SinConexion(), comandos).Options);

        Assert.Null(await ReglasTaller.ValidarTelefono(db, "88881111", idCliente: 1));
        Assert.Null(await ReglasTaller.ValidarTelefono(db, "88881111", idTrabajador: 1));
        Assert.Equal(2, comandos.Sql.Count);
        Assert.All(comandos.Sql, sql =>
        {
            Assert.Contains("[dbo].[Cliente]", sql);
            Assert.Contains("[dbo].[Trabajador]", sql);
            Assert.Contains("UNION ALL", sql);
            Assert.Contains("REPLACE", sql);
        });
    }

    [Fact]
    public async Task Consultas_de_listados_y_reportes_se_traducen_con_el_nuevo_esquema()
    {
        var comandos = new ComandosVacios();
        using var db = new TallerDbContext(new DbContextOptionsBuilder<TallerDbContext>()
            .UseSqlServer("Server=unused;Database=unused;User Id=unused;Password=unused")
            .AddInterceptors(new SinConexion(), comandos).Options);
        var desde = new DateOnly(2026, 1, 1);
        var hasta = new DateOnly(2026, 12, 31);
        var reportes = new ReporteService(db);
        await reportes.ServiciosPorPeriodo(desde, hasta, "Mantenimiento", "Abierto", 1, 1);
        await reportes.ServiciosPorTipo(desde, hasta);
        await reportes.ServiciosPorEstado(desde, hasta);
        await reportes.ServiciosPorVehiculo(desde, hasta);
        await reportes.HistorialPorCliente(1);
        await reportes.TrabajosPorTrabajador();
        await reportes.CargaActual();
        await reportes.ProductosMasUtilizados(desde, hasta);
        await reportes.FacturasPorPeriodo(desde, hasta);
        await reportes.TotalFacturadoPorPeriodo(desde, hasta);
        await new ServicioService(db).CargasDeTrabajadores([1]);
        await ReglasTaller.Carga(db, 1);
        await new ServicioService(db).Buscar("Toyota", "Mantenimiento", "Abierto", 1, 1, 10);
        await new ClienteService(db).Buscar("Juan", true, 1, 10);
        await new VehiculoService(db).Buscar("MGA", 1, true, 1, 10);
        await new ProductoService(db).Buscar("Filtro", true, 1, 10);
        var facturas = new FacturacionService(db, FabricaDeContextos.Configuracion());
        await facturas.Buscar("FAC", 1, desde, hasta, 1, 10);
        await facturas.ServiciosElegibles(1);
        Assert.True(comandos.Sql.Count >= 20);
        Assert.Contains(comandos.Sql, s => s.Contains("[dbo].[Productos_Utilizados]") && s.Contains("GROUP BY"));
        Assert.DoesNotContain(comandos.Sql, s => s.Contains("[Areas]") || s.Contains("[IdDetalleFactura]"));
    }

    private class SinConexion : DbConnectionInterceptor
    {
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection,
            ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(InterceptionResult.Suppress());
    }

    private class ComandosVacios : DbCommandInterceptor
    {
        public List<string> Sql { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Sql.Add(command.CommandText);
            var tabla = new DataTable();
            if (command.CommandText.StartsWith("SELECT COUNT("))
            {
                tabla.Columns.Add("Value", typeof(int));
                tabla.Rows.Add(0);
            }
            else if (command.CommandText.StartsWith("SELECT CASE") && command.CommandText.Contains("EXISTS"))
            {
                tabla.Columns.Add("Value", typeof(bool));
                tabla.Rows.Add(false);
            }
            return ValueTask.FromResult(InterceptionResult<DbDataReader>.SuppressWithResult(tabla.CreateDataReader()));
        }
    }
}
