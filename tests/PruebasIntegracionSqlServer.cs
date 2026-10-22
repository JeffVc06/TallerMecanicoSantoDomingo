using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TallerMecanicoSantoDomingo.Data;
using TallerMecanicoSantoDomingo.Models;
using TallerMecanicoSantoDomingo.Services;
using Xunit;

namespace TallerMecanicoSantoDomingo.Tests;

/// <summary>Verifica restricciones y operaciones que dependen de SQL Server.</summary>
[Trait("Categoria", "Integracion")]
public class PruebasIntegracionSqlServerTests
{
    private static readonly string? CadenaConexion =
        Environment.GetEnvironmentVariable("ConnectionStrings__TallerDB")
        ?? Environment.GetEnvironmentVariable("TALLERMECANICO_CADENA");

    private static bool? _disponible;

    private static bool ServidorDisponible()
    {
        if (_disponible.HasValue)
            return _disponible.Value;

        if (string.IsNullOrWhiteSpace(CadenaConexion))
            return false;

        try
        {
            using var conexion = new SqlConnection(CadenaConexion);
            conexion.Open();
            _disponible = true;
        }
        catch
        {
            _disponible = false;
        }
        return _disponible.Value;
    }

    private static TallerDbContext NuevoContexto()
    {
        var opciones = new DbContextOptionsBuilder<TallerDbContext>()
            .UseSqlServer(CadenaConexion)
            .Options;
        return new TallerDbContext(opciones);
    }

    private static async Task<object?> EscalarAsync(string sql, params (string, object?)[] parametros)
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var comando = new SqlCommand(sql, conexion);
        foreach (var (nombre, valor) in parametros)
            comando.Parameters.AddWithValue(nombre, valor ?? DBNull.Value);
        return await comando.ExecuteScalarAsync();
    }

    private static async Task<int> EjecutarAsync(string sql, params (string, object?)[] parametros)
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var comando = new SqlCommand(sql, conexion);
        foreach (var (nombre, valor) in parametros)
            comando.Parameters.AddWithValue(nombre, valor ?? DBNull.Value);
        return await comando.ExecuteNonQueryAsync();
    }

    private static async Task<List<string>> ListaAsync(string sql)
    {
        var resultado = new List<string>();
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var comando = new SqlCommand(sql, conexion);
        await using var lector = await comando.ExecuteReaderAsync();
        while (await lector.ReadAsync())
            resultado.Add(lector.GetString(0));
        return resultado;
    }
    [SkippableFact]
    public async Task Esquema_contiene_las_nueve_tablas_del_diagrama_y_Usuarios()
    {
        Skip.IfNot(ServidorDisponible(), "SQL Server no está disponible; prueba de integración omitida.");

        var tablas = await ListaAsync("SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'");

        string[] esperadas =
        {
            "Cliente", "Vehiculo", "Trabajador", "Servicio",
            "Detalle_Servicio", "Producto", "Productos_Utilizados", "Factura",
            "Detalle_Factura", "Usuarios"
        };

        foreach (var tabla in esperadas)
            Assert.Contains(tabla, tablas);
    }

    [SkippableFact]
    public async Task Restriccion_Check_rechaza_un_producto_con_precio_negativo()
    {
        Skip.IfNot(ServidorDisponible(), "SQL Server no está disponible; prueba de integración omitida.");

        var excepcion = await Assert.ThrowsAsync<SqlException>(async () =>
            await EjecutarAsync(
                "INSERT INTO dbo.Producto (Nombre, Descripcion, Precio, Stock, Activo, FechaRegistro) " +
                "VALUES (N'PRUEBA-PRECIO', N'no debe insertarse', -1, 0, 1, SYSUTCDATETIME())"));

        Assert.Contains("CK_Producto_Precio", excepcion.Message);
    }

    [SkippableFact]
    public async Task Restriccion_Check_rechaza_un_servicio_con_fecha_fin_anterior()
    {
        Skip.IfNot(ServidorDisponible(), "SQL Server no está disponible; prueba de integración omitida.");

        var idVehiculo = await EscalarAsync("SELECT TOP (1) IdVehiculo FROM dbo.Vehiculo ORDER BY IdVehiculo");
        Skip.If(idVehiculo is null, "No hay vehículos en la base de datos para ejecutar la comprobación.");

        var excepcion = await Assert.ThrowsAsync<SqlException>(async () =>
            await EjecutarAsync(
                "INSERT INTO dbo.Servicio (IdVehiculo, Tipo, Descripcion, Estado, FechaInicio, FechaFin, Costo, FechaRegistro) " +
                "VALUES (@idVehiculo, N'Mantenimiento', N'PRUEBA-FECHAS', N'Abierto', '2026-09-10', '2026-09-01', 100, SYSUTCDATETIME())",
                ("@idVehiculo", idVehiculo)));

        Assert.Contains("CK_Servicio_FechaFin", excepcion.Message);
    }
    // Regla 7 con concurrencia real: tres asignaciones simultáneas → solo dos prosperan
    [SkippableFact]
    public async Task Regla7_Concurrencia_real_no_permite_mas_de_dos_servicios_activos()
    {
        Skip.IfNot(ServidorDisponible(), "SQL Server no está disponible; prueba de integración omitida.");

        var token = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        int idCliente = 0, idVehiculo = 0, idTrabajador = 0;

        try
        {
            // Preparación: cliente, vehículo, trabajador de mecánica y tres servicios abiertos.
            await using (var conexion = new SqlConnection(CadenaConexion))
            {
                await conexion.OpenAsync();

                var preparacion = @"
INSERT INTO dbo.Cliente (Nombre, Apellido, Telefono, Direccion, RUC, Activo, FechaRegistro)
VALUES (N'Prueba', @token, N'0000-0000', N'Prueba', N'TST' + @token, 1, SYSUTCDATETIME());
DECLARE @idCliente INT = SCOPE_IDENTITY();

INSERT INTO dbo.Vehiculo (IdCliente, Placa, Marca, Modelo, Anio, Activo, FechaRegistro)
VALUES (@idCliente, N'T' + @token, N'Marca', N'Modelo', 2020, 1, SYSUTCDATETIME());
DECLARE @idVehiculo INT = SCOPE_IDENTITY();

INSERT INTO dbo.Trabajador (Nombre, Apellido, Telefono, Area, Estado, FechaIngreso)
VALUES (N'Prueba', @token, @token, N'Mantenimiento mecánico', 1, SYSUTCDATETIME());
DECLARE @idTrabajador INT = SCOPE_IDENTITY();

INSERT INTO dbo.Servicio (IdVehiculo, Tipo, Descripcion, Estado, FechaInicio, Costo, FechaRegistro)
SELECT @idVehiculo, N'Mantenimiento', N'PRUEBA ' + @token + N' ' + CAST(v.n AS NVARCHAR(2)), N'Abierto', '2026-10-01', 100.00, SYSUTCDATETIME()
FROM (VALUES (1),(2),(3)) AS v(n);

SELECT @idCliente, @idVehiculo, @idTrabajador;";

                await using var comando = new SqlCommand(preparacion, conexion);
                comando.Parameters.AddWithValue("@token", token);
                await using var lector = await comando.ExecuteReaderAsync();
                Assert.True(await lector.ReadAsync());
                idCliente = lector.GetInt32(0);
                idVehiculo = lector.GetInt32(1);
                idTrabajador = lector.GetInt32(2);
            }

            var idsServicios = new List<int>();
            await using (var conexion = new SqlConnection(CadenaConexion))
            {
                await conexion.OpenAsync();
                await using var comando = new SqlCommand(
                    "SELECT IdServicio FROM dbo.Servicio WHERE Descripcion LIKE N'PRUEBA ' + @token + N' %' ORDER BY IdServicio", conexion);
                comando.Parameters.AddWithValue("@token", token);
                await using var lector = await comando.ExecuteReaderAsync();
                while (await lector.ReadAsync())
                    idsServicios.Add(lector.GetInt32(0));
            }
            Assert.Equal(3, idsServicios.Count);

            // Tres asignaciones simultáneas con contextos independientes.
            using var barrera = new Barrier(idsServicios.Count);
            var tareas = idsServicios.Select(idServicio => Task.Run(async () =>
            {
                await using var contexto = NuevoContexto();
                var servicioAsignacion = new AsignacionService(contexto);
                barrera.SignalAndWait();
                return await servicioAsignacion.Asignar(idServicio, idTrabajador, "Prueba de concurrencia");
            })).ToArray();

            var resultados = await Task.WhenAll(tareas);
            var exitosas = resultados.Count(r => r.EsCorrecto);

            Assert.Equal(2, exitosas);

            var activos = Convert.ToInt32(await EscalarAsync(
                "SELECT COUNT(*) FROM dbo.Detalle_Servicio WHERE IdTrabajador = @idTrabajador AND Estado = 'Activo'",
                ("@idTrabajador", idTrabajador)));
            Assert.Equal(2, activos);
        }
        finally
        {
            // Limpieza: se eliminan únicamente los datos creados por esta prueba.
            await EjecutarAsync(@"
DELETE FROM dbo.Detalle_Servicio WHERE IdTrabajador = @idTrabajador;
DELETE FROM dbo.Servicio       WHERE IdVehiculo   = @idVehiculo;
DELETE FROM dbo.Vehiculo       WHERE IdVehiculo   = @idVehiculo;
DELETE FROM dbo.Trabajador    WHERE IdTrabajador = @idTrabajador;
DELETE FROM dbo.Cliente        WHERE IdCliente    = @idCliente;",
                ("@idTrabajador", idTrabajador),
                ("@idVehiculo", idVehiculo),
                ("@idCliente", idCliente));
        }
    }
    // Regla 14 sobre SQL Server: número de factura real y anulación con el procedimiento
    [SkippableFact]
    public async Task Regla14_Factura_se_genera_y_se_anula_con_el_procedimiento_almacenado()
    {
        Skip.IfNot(ServidorDisponible(), "SQL Server no está disponible; prueba de integración omitida.");

        var token = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        int idCliente = 0, idVehiculo = 0, idServicio = 0;

        try
        {
            await using (var conexion = new SqlConnection(CadenaConexion))
            {
                await conexion.OpenAsync();
                var preparacion = @"
INSERT INTO dbo.Cliente (Nombre, Apellido, Telefono, Direccion, RUC, Activo, FechaRegistro)
VALUES (N'Prueba', @token, N'0000-0000', N'Prueba', N'FCT' + @token, 1, SYSUTCDATETIME());
DECLARE @idCliente INT = SCOPE_IDENTITY();

INSERT INTO dbo.Vehiculo (IdCliente, Placa, Marca, Modelo, Anio, Activo, FechaRegistro)
VALUES (@idCliente, N'F' + @token, N'Marca', N'Modelo', 2020, 1, SYSUTCDATETIME());
DECLARE @idVehiculo INT = SCOPE_IDENTITY();

INSERT INTO dbo.Servicio (IdVehiculo, Tipo, Descripcion, Estado, FechaInicio, FechaFin, Costo, FechaRegistro)
VALUES (@idVehiculo, N'Mantenimiento', N'PRUEBA-FACTURA ' + @token, N'Finalizado', '2026-09-01', '2026-09-05', 1000.00, SYSUTCDATETIME());
DECLARE @idServicio INT = SCOPE_IDENTITY();

SELECT @idCliente, @idVehiculo, @idServicio;";
                await using var comando = new SqlCommand(preparacion, conexion);
                comando.Parameters.AddWithValue("@token", token);
                await using var lector = await comando.ExecuteReaderAsync();
                Assert.True(await lector.ReadAsync());
                idCliente = lector.GetInt32(0);
                idVehiculo = lector.GetInt32(1);
                idServicio = lector.GetInt32(2);
            }

            await using var contexto = NuevoContexto();
            var facturacion = new FacturacionService(contexto, FabricaDeContextos.Configuracion(15m));

            var creada = await facturacion.CrearFactura(idCliente, "Efectivo", new List<int> { idServicio }, null);
            Assert.True(creada.EsCorrecto, creada.Mensaje);
            Assert.Matches(@"^FAC-\d{4}-\d{4}$", creada.Datos!.NumeroFactura);
            Assert.Equal(1150m, creada.Datos.Total); // 1000 de subtotal + 15 % de IVA = 1150

            var anulada = await facturacion.AnularFactura(creada.Datos.IdFactura, "Prueba de integración");
            Assert.True(anulada.EsCorrecto, anulada.Mensaje);

            var estado = (string?)await EscalarAsync("SELECT Estado FROM dbo.Factura WHERE IdFactura = @id",
                ("@id", creada.Datos.IdFactura));
            Assert.Equal("Anulada", estado);

            // El registro sigue existiendo (no hubo borrado físico).
            var existe = Convert.ToInt32(await EscalarAsync("SELECT COUNT(*) FROM dbo.Factura WHERE IdFactura = @id",
                ("@id", creada.Datos.IdFactura)));
            Assert.Equal(1, existe);
        }
        finally
        {
            await EjecutarAsync(@"
DELETE df FROM dbo.Detalle_Factura df JOIN dbo.Servicio s ON s.IdServicio = df.IdServicio
    WHERE s.IdVehiculo = @idVehiculo;
DELETE FROM dbo.Factura        WHERE IdCliente    = @idCliente;
DELETE FROM dbo.Servicio       WHERE IdVehiculo   = @idVehiculo;
DELETE FROM dbo.Vehiculo       WHERE IdVehiculo   = @idVehiculo;
DELETE FROM dbo.Cliente        WHERE IdCliente    = @idCliente;",
                ("@idVehiculo", idVehiculo),
                ("@idCliente", idCliente));
            _ = idServicio;
        }
    }
}
