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
        Environment.GetEnvironmentVariable("TALLERMECANICO_CADENA_PRUEBAS");

    private static bool? _disponible;

    private static bool ServidorDisponible()
    {
        if (_disponible.HasValue)
            return _disponible.Value;

        if (Environment.GetEnvironmentVariable("TALLERMECANICO_PRUEBAS_ESCRITURA") != "1" || string.IsNullOrWhiteSpace(CadenaConexion))
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
    public async Task Esquema_sql_coincide_con_las_tablas_columnas_y_claves_del_modelo()
    {
        Skip.IfNot(ServidorDisponible(), "SQL Server no está disponible; prueba de integración omitida.");

        await using var contexto = NuevoContexto();
        var modelo = contexto.Model.GetRelationalModel();
        var columnas = await ListaAsync("""
            SELECT CONCAT(t.name, '.', c.name, ':',
                CASE TYPE_NAME(c.user_type_id)
                    WHEN 'nvarchar' THEN CONCAT('nvarchar(', IIF(c.max_length = -1, 'max', CONVERT(varchar(5), c.max_length / 2)), ')')
                    WHEN 'decimal' THEN CONCAT('decimal(', c.precision, ',', c.scale, ')')
                    ELSE TYPE_NAME(c.user_type_id)
                END, ':', c.is_nullable)
            FROM sys.tables t
            JOIN sys.columns c ON c.object_id = t.object_id
            WHERE t.schema_id = SCHEMA_ID(N'dbo');
            """);
        var columnasEsperadas = modelo.Tables.SelectMany(t =>
            t.Columns.Select(c => $"{t.Name}.{c.Name}:{c.StoreType}:{(c.IsNullable ? 1 : 0)}"));
        Assert.Equal(columnasEsperadas.Order(StringComparer.Ordinal), columnas.Order(StringComparer.Ordinal));

        var primarias = await ListaAsync("""
            SELECT CONCAT(t.name, '.', c.name, ':', ic.key_ordinal)
            FROM sys.tables t
            JOIN sys.indexes i ON i.object_id = t.object_id AND i.is_primary_key = 1
            JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            WHERE t.schema_id = SCHEMA_ID(N'dbo');
            """);
        var primariasEsperadas = modelo.Tables.SelectMany(t =>
            t.PrimaryKey!.Columns.Select((c, i) => $"{t.Name}.{c.Name}:{i + 1}"));
        Assert.Equal(primariasEsperadas.Order(StringComparer.Ordinal), primarias.Order(StringComparer.Ordinal));

        var foraneas = await ListaAsync("""
            SELECT CONCAT(t.name, '.', c.name, '->', p.name, '.', pc.name)
            FROM sys.foreign_keys fk
            JOIN sys.foreign_key_columns fc ON fc.constraint_object_id = fk.object_id
            JOIN sys.tables t ON t.object_id = fc.parent_object_id
            JOIN sys.columns c ON c.object_id = t.object_id AND c.column_id = fc.parent_column_id
            JOIN sys.tables p ON p.object_id = fc.referenced_object_id
            JOIN sys.columns pc ON pc.object_id = p.object_id AND pc.column_id = fc.referenced_column_id
            WHERE t.schema_id = SCHEMA_ID(N'dbo') AND fk.is_disabled = 0 AND fk.is_not_trusted = 0
              AND fk.delete_referential_action = 0;
            """);
        var foraneasEsperadas = modelo.Tables.SelectMany(t => t.ForeignKeyConstraints)
            .SelectMany(f => f.Columns.Zip(f.PrincipalColumns,
                (c, pc) => $"{f.Table.Name}.{c.Name}->{f.PrincipalTable.Name}.{pc.Name}"));
        Assert.Equal(foraneasEsperadas.Order(StringComparer.Ordinal), foraneas.Order(StringComparer.Ordinal));
    }

    [SkippableFact]
    public async Task Clientes_admiten_varios_ruc_nulos_y_exigen_una_identificacion_unica()
    {
        Skip.IfNot(ServidorDisponible(), "SQL Server no está disponible; prueba de integración omitida.");
        var token = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var transaccion = (SqlTransaction)await conexion.BeginTransactionAsync();
        async Task Insertar(string? cedula, string? ruc)
        {
            await using var comando = new SqlCommand("""
                INSERT INTO dbo.Cliente (Nombre, Apellido, Telefono, Direccion, Cedula, RUC)
                VALUES (N'Prueba', N'Identificación', N'8888-1111', N'Managua', @cedula, @ruc);
                """, conexion, transaccion);
            comando.Parameters.AddWithValue("@cedula", (object?)cedula ?? DBNull.Value);
            comando.Parameters.AddWithValue("@ruc", (object?)ruc ?? DBNull.Value);
            await comando.ExecuteNonQueryAsync();
        }

        try
        {
            await Insertar("C1" + token, null);
            await Insertar("C2" + token, null);
            await Insertar(null, "J" + token);
            var duplicada = await Assert.ThrowsAsync<SqlException>(() => Insertar("C1" + token, null));
            Assert.Equal(2601, duplicada.Number);
            var rucDuplicado = await Assert.ThrowsAsync<SqlException>(() => Insertar(null, "J" + token));
            Assert.Equal(2601, rucDuplicado.Number);
            var sinIdentificacion = await Assert.ThrowsAsync<SqlException>(() => Insertar(" ", null));
            Assert.Contains("CK_Cliente_Identificacion", sinIdentificacion.Message);
        }
        finally
        {
            await transaccion.RollbackAsync();
        }
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

INSERT INTO dbo.Servicio (IdVehiculo, Tipo, Descripcion, Estado, FechaInicio, FechaFin, Costo, FechaRegistro, DescripcionFalla)
VALUES (@idVehiculo, N'Mantenimiento', N'PRUEBA-FACTURA ' + @token, N'Finalizado', '2026-09-01', '2026-09-05', 1000.00, SYSUTCDATETIME(), N'Falla de prueba');
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

    [SkippableFact]
    public async Task Dos_consumos_simultaneos_no_sobrevenden_y_reintentos_no_duplican_descuentos()
    {
        Skip.IfNot(ServidorDisponible(), "Se necesita una base de pruebas SQL Server existente y autorización de escritura de pruebas.");
        var escenario = await PrepararEscenario(finalizados: false);
        try
        {
            using var barrera = new Barrier(2);
            var tareas = escenario.Servicios.Take(2).Select(id => Task.Run(async () =>
            {
                await using var contexto = NuevoContexto();
                barrera.SignalAndWait();
                return (id, resultado: await new ProductoService(contexto).AgregarAlServicio(id, escenario.Producto, 1m));
            })).ToArray();
            var resultados = await Task.WhenAll(tareas);
            var ganador = Assert.Single(resultados, r => r.resultado.EsCorrecto);
            Assert.Equal(0.5m, Convert.ToDecimal(await EscalarAsync("SELECT Stock FROM dbo.Producto WHERE IdProducto=@id", ("@id", escenario.Producto))));
            await using var db = NuevoContexto();
            var productos = new ProductoService(db);
            Assert.False((await productos.AgregarAlServicio(ganador.id, escenario.Producto, 1)).EsCorrecto);
            Assert.True((await productos.ActualizarCantidad(ganador.id, escenario.Producto, 1.25m)).EsCorrecto);
            Assert.True((await productos.ActualizarCantidad(ganador.id, escenario.Producto, 1.25m)).EsCorrecto);
            Assert.Equal(0.25m, (await productos.ObtenerPorId(escenario.Producto))!.Stock);
        }
        finally { await LimpiarEscenario(escenario); }
    }

    [SkippableFact]
    public async Task Facturacion_simultanea_no_duplica_servicio_ni_numeros()
    {
        Skip.IfNot(ServidorDisponible(), "Se necesita una base de pruebas SQL Server existente y autorización de escritura de pruebas.");
        var escenario = await PrepararEscenario(finalizados: true);
        try
        {
            using var barrera = new Barrier(2);
            var tareas = Enumerable.Range(0, 2).Select(_ => Task.Run(async () =>
            {
                await using var contexto = NuevoContexto();
                barrera.SignalAndWait();
                return await new FacturacionService(contexto, FabricaDeContextos.Configuracion())
                    .CrearFactura(escenario.Cliente, "Efectivo", [escenario.Servicios[0]], null);
            })).ToArray();
            Assert.Single(await Task.WhenAll(tareas), r => r.EsCorrecto);
            var siguientes = escenario.Servicios.Skip(1).Select(id => Task.Run(async () =>
            {
                await using var contexto = NuevoContexto();
                return await new FacturacionService(contexto, FabricaDeContextos.Configuracion())
                    .CrearFactura(escenario.Cliente, "Efectivo", [id], null);
            }));
            Assert.All(await Task.WhenAll(siguientes), r => Assert.True(r.EsCorrecto, r.Mensaje));
            await using var db = NuevoContexto();
            var facturas = await db.Facturas.Where(f => f.IdCliente == escenario.Cliente).ToListAsync();
            Assert.Equal(3, facturas.Count);
            Assert.Equal(3, facturas.Select(f => f.NumeroFactura).Distinct().Count());
            Assert.All(facturas, f => Assert.Equal(115m, f.Total));
        }
        finally { await LimpiarEscenario(escenario); }
    }

    [SkippableTheory]
    [InlineData("Clientes")]
    [InlineData("Vehiculos")]
    [InlineData("Trabajadores")]
    [InlineData("Productos")]
    public async Task Desactivar_y_reactivar_se_refleja_en_otra_conexion_sql(string modulo)
    {
        Skip.IfNot(ServidorDisponible(), "Se necesita una base de pruebas SQL Server existente y autorización de escritura de pruebas.");
        var escenario = await PrepararEscenario(finalizados: true);
        var idTrabajador = 0;
        try
        {
            await using (var db = NuevoContexto())
            {
                var trabajador = new Trabajador { Nombre = "Prueba", Apellido = "Estado", Telefono = "8888-1111" };
                db.Trabajadores.Add(trabajador);
                await db.SaveChangesAsync();
                idTrabajador = trabajador.IdTrabajador;
            }
            var (tabla, columnaId, columnaEstado, id) = modulo switch
            {
                "Clientes" => ("Cliente", "IdCliente", "Activo", escenario.Cliente),
                "Vehiculos" => ("Vehiculo", "IdVehiculo", "Activo", escenario.Vehiculo),
                "Trabajadores" => ("Trabajador", "IdTrabajador", "Estado", idTrabajador),
                _ => ("Producto", "IdProducto", "Activo", escenario.Producto)
            };
            foreach (var activo in new[] { false, true, false, true })
            {
                await using var db = NuevoContexto();
                var resultado = modulo switch
                {
                    "Clientes" => await new ClienteService(db).CambiarEstado(id, activo),
                    "Vehiculos" => await new VehiculoService(db).CambiarEstado(id, activo),
                    "Trabajadores" => await new TrabajadorService(db).CambiarEstado(id, activo),
                    _ => await new ProductoService(db).CambiarEstado(id, activo)
                };
                Assert.True(resultado.EsCorrecto, resultado.Mensaje);
                // Solo nombres fijos del switch; el identificador va parametrizado.
                var persistido = await EscalarAsync($"SELECT [{columnaEstado}] FROM dbo.[{tabla}] WHERE [{columnaId}]=@id", ("@id", id));
                Assert.Equal(activo, Convert.ToBoolean(persistido));
            }
        }
        finally
        {
            await EjecutarAsync("DELETE FROM dbo.Trabajador WHERE IdTrabajador=@id", ("@id", idTrabajador));
            await LimpiarEscenario(escenario);
        }
    }

    private record Escenario(int Cliente, int Vehiculo, int Producto, List<int> Servicios);
    private static async Task<Escenario> PrepararEscenario(bool finalizados)
    {
        await using var db = NuevoContexto();
        var token = Guid.NewGuid().ToString("N")[..12];
        var cliente = new Cliente { Nombre = "Prueba", Apellido = token, Direccion = "Managua", Telefono = "8888-1111", RFC = "TST" + token };
        var vehiculo = new Vehiculo { Cliente = cliente, Placa = "TST" + token, Marca = "Prueba", Modelo = "Prueba", Anio = 2020 };
        var producto = new Producto { Nombre = "Prueba " + token, Descripcion = "Verificación de existencias", PrecioUnitario = 10m, Stock = 1.5m };
        db.AddRange(vehiculo, producto);
        var servicios = Enumerable.Range(0, 3).Select(_ => new Servicio { Vehiculo = vehiculo, TipoServicio = "Mantenimiento",
            Descripcion = "Prueba " + token, DescripcionFalla = "Falla de prueba", Total = 100m,
            Estado = finalizados ? "Finalizado" : "Abierto", FechaInicio = new DateOnly(2026, 9, 1),
            FechaFin = finalizados ? new DateOnly(2026, 9, 2) : null }).ToList();
        db.Servicios.AddRange(servicios);
        await db.SaveChangesAsync();
        return new Escenario(cliente.IdCliente, vehiculo.IdVehiculo, producto.IdProducto, servicios.Select(s => s.IdServicio).ToList());
    }
    private static Task<int> LimpiarEscenario(Escenario e) => EjecutarAsync("""
        DELETE d FROM dbo.Detalle_Factura d JOIN dbo.Factura f ON f.IdFactura=d.IdFactura WHERE f.IdCliente=@cliente;
        DELETE FROM dbo.Factura WHERE IdCliente=@cliente;
        DELETE FROM dbo.Productos_Utilizados WHERE IdProducto=@producto;
        DELETE FROM dbo.Servicio WHERE IdVehiculo=@vehiculo;
        DELETE FROM dbo.Producto WHERE IdProducto=@producto;
        DELETE FROM dbo.Vehiculo WHERE IdVehiculo=@vehiculo;
        DELETE FROM dbo.Cliente WHERE IdCliente=@cliente;
        """, ("@cliente", e.Cliente), ("@producto", e.Producto), ("@vehiculo", e.Vehiculo));
}
