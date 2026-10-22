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
            if (command.CommandText.StartsWith("SELECT COUNT(*)"))
            {
                tabla.Columns.Add("Value", typeof(int));
                tabla.Rows.Add(0);
            }
            return ValueTask.FromResult(InterceptionResult<DbDataReader>.SuppressWithResult(tabla.CreateDataReader()));
        }
    }
}
