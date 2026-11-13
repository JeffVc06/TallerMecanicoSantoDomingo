using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using TallerMecanicoSantoDomingo.Models;
using TallerMecanicoSantoDomingo.Services;
using TallerMecanicoSantoDomingo.ViewModels;
using Xunit;

namespace TallerMecanicoSantoDomingo.Tests;

public class PruebasFormatosEntrada
{
    [Theory]
    [InlineData("12345", false)]
    [InlineData("Ana123", false)]
    [InlineData("***", false)]
    [InlineData("Ana<script>", false)]
    [InlineData("Ana\tMaría", false)]
    [InlineData("Ana\nMaría", false)]
    [InlineData("-Ana", false)]
    [InlineData("José Ángel", true)]
    [InlineData("María-José", true)]
    [InlineData("O'Connor", true)]
    [InlineData("D’Ávila", true)]
    [InlineData("Jose\u0301", true)]
    [InlineData("李", true)]
    [InlineData("  De la Cruz  ", true)]
    public async Task Nombres_personales_se_validan_en_formularios_y_en_altas_y_ediciones(string valor, bool valido)
    {
        foreach (var esCliente in new[] { true, false })
        foreach (var apellido in new[] { true, false })
        {
            using var db = FabricaDeContextos.EnMemoria();
            FabricaDeContextos.SembrarBase(db);
            var campo = apellido ? "Apellido" : "Nombre";
            var nombre = apellido ? "Ana" : valor;
            var apellidos = apellido ? valor : "López";
            object formulario = esCliente
                ? new ClienteFormViewModel { Nombre = nombre, Apellido = apellidos, Telefono = "81234567", Direccion = "Managua", Cedula = "FORMATO" }
                : new TrabajadorFormViewModel { Nombre = nombre, Apellido = apellidos, Telefono = "81234567", Area = "Pintura" };
            ComprobarFormulario(formulario, valido, campo);
            if (esCliente)
            {
                var clientes = new ClienteService(db);
                var entrada = new Cliente { Nombre = nombre, Apellido = apellidos, Telefono = "81234567", Direccion = "Managua", Cedula = "FORMATO" };
                Assert.Equal(valido, (await clientes.Crear(entrada)).EsCorrecto);
                entrada.IdCliente = 1;
                entrada.Telefono = "88881111";
                entrada.Cedula = "EDICION-FORMATO";
                Assert.Equal(valido, (await clientes.Editar(entrada)).EsCorrecto);
                var guardado = (await clientes.ObtenerPorId(1))!;
                Assert.Equal(valido ? nombre.Trim() : "Juan", guardado.Nombre);
                Assert.Equal(valido ? apellidos.Trim() : "Pérez López", guardado.Apellido);
            }
            else
            {
                var trabajadores = new TrabajadorService(db);
                var entrada = new Trabajador { Nombre = nombre, Apellido = apellidos, Telefono = "81234567", Area = "Pintura" };
                Assert.Equal(valido, (await trabajadores.Guardar(entrada, true)).EsCorrecto);
                entrada.IdTrabajador = 1;
                entrada.Telefono = "85554444";
                Assert.Equal(valido, (await trabajadores.Guardar(entrada, false)).EsCorrecto);
                var guardado = await db.Trabajadores.AsNoTracking().SingleAsync(t => t.IdTrabajador == 1);
                Assert.Equal(valido ? nombre.Trim() : "Pedro", guardado.Nombre);
                Assert.Equal(valido ? apellidos.Trim() : "Martínez", guardado.Apellido);
            }
        }
    }

    [Theory]
    [InlineData("---", false)]
    [InlineData("...", false)]
    [InlineData("\u200b", false)]
    [InlineData("Texto\0oculto", false)]
    [InlineData("10W-30", true)]
    [InlineData("Cambio de aceite.\nRevisar filtro.", true)]
    public async Task Textos_con_contenido_real_se_exigen_sin_restringir_referencias_y_descripciones(string valor, bool valido)
    {
        using var db = FabricaDeContextos.EnMemoria();
        FabricaDeContextos.SembrarBase(db);
        var productos = new ProductoService(db);
        ComprobarFormulario(new ProductoFormViewModel { Nombre = "Aceite", Descripcion = valor }, valido, "Descripcion");
        Assert.Equal(valido, (await productos.Crear(new Producto { Nombre = "Aceite", Descripcion = valor })).EsCorrecto);
        var producto = (await productos.ObtenerPorId(1))!;
        producto.Descripcion = valor;
        Assert.Equal(valido, (await productos.Editar(producto, producto.Stock)).EsCorrecto);
        Assert.Equal(valido ? valor : "Filtro", (await productos.ObtenerPorId(1))!.Descripcion);

        ComprobarFormulario(new ClienteFormViewModel { Nombre = "Ana", Apellido = "López", Telefono = "88881111", Direccion = valor, Cedula = "FORMATO" }, valido, "Direccion");
        var clientes = new ClienteService(db);
        var cliente = (await clientes.ObtenerPorId(1))!;
        cliente.Direccion = valor;
        Assert.Equal(valido, (await clientes.Editar(cliente)).EsCorrecto);
        Assert.Equal(valido ? valor : "Managua", (await clientes.ObtenerPorId(1))!.Direccion);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("   ", true)]
    [InlineData("mecanico@taller.example", true)]
    [InlineData(" nombre+avisos@sub.dominio.com ", true)]
    [InlineData("correo@dominio", false)]
    [InlineData("a..b@dominio.com", false)]
    [InlineData(".ana@dominio.com", false)]
    [InlineData("ana.@dominio.com", false)]
    [InlineData("ana@dominio..com", false)]
    [InlineData("ana@-dominio.com", false)]
    [InlineData("ana@dominio-.com", false)]
    [InlineData("ana@dominio_com.ni", false)]
    [InlineData("ana lopez@dominio.com", false)]
    [InlineData("ana@dominio.com\notro", false)]
    public async Task Correo_opcional_exige_usuario_y_dominio_completo_al_crear_y_editar(string? correo, bool valido)
    {
        ComprobarFormulario(new TrabajadorFormViewModel { Nombre = "Ana", Apellido = "López", Telefono = "81234567", Area = "Pintura", Correo = correo }, valido, "Correo");
        foreach (var nuevo in new[] { true, false })
        {
            using var db = FabricaDeContextos.EnMemoria();
            FabricaDeContextos.SembrarBase(db);
            var entrada = await db.Trabajadores.AsNoTracking().SingleAsync(t => t.IdTrabajador == 1);
            entrada.IdTrabajador = nuevo ? 0 : 1;
            entrada.Telefono = nuevo ? "81234567" : "85554444";
            entrada.Correo = correo;
            Assert.Equal(valido, (await new TrabajadorService(db).Guardar(entrada, nuevo)).EsCorrecto);
            if (valido)
            {
                var guardado = await db.Trabajadores.AsNoTracking().SingleAsync(t => t.IdTrabajador == entrada.IdTrabajador);
                Assert.Equal(string.IsNullOrWhiteSpace(correo) ? null : correo.Trim().ToLowerInvariant(), guardado.Correo);
            }
            else
            {
                Assert.Equal(2, await db.Trabajadores.CountAsync());
                Assert.All(await db.Trabajadores.AsNoTracking().ToListAsync(), t => Assert.Null(t.Correo));
            }
        }
    }

    [Fact]
    public async Task Correo_previo_con_espacios_y_mayusculas_sigue_reservado_al_desactivar_al_titular()
    {
        using var db = FabricaDeContextos.EnMemoria();
        FabricaDeContextos.SembrarBase(db);
        var titular = (await db.Trabajadores.FindAsync(1))!;
        titular.Correo = " MECANICO@TALLER.EXAMPLE ";
        titular.Activo = false;
        await db.SaveChangesAsync();
        var trabajadores = new TrabajadorService(db);
        var otro = await db.Trabajadores.AsNoTracking().SingleAsync(t => t.IdTrabajador == 2);
        otro.Correo = "mecanico@taller.example";
        var resultado = await trabajadores.Guardar(otro, false);
        Assert.False(resultado.EsCorrecto);
        Assert.Contains("correo ya está registrado", resultado.Mensaje);
        Assert.Null((await db.Trabajadores.AsNoTracking().SingleAsync(t => t.IdTrabajador == 2)).Correo);
        var propia = await db.Trabajadores.AsNoTracking().SingleAsync(t => t.IdTrabajador == 1);
        Assert.True((await trabajadores.Guardar(propia, false)).EsCorrecto);
    }

    [Theory]
    [InlineData(null, false, true)]
    [InlineData("", false, true)]
    [InlineData("   ", false, true)]
    [InlineData("---", false, false)]
    [InlineData("M/778899", false, false)]
    [InlineData("M_778899", false, false)]
    [InlineData("M-778899-", false, false)]
    [InlineData("M-778899", true, true)]
    [InlineData(" m 778899 ", true, true)]
    [InlineData("ABCD123", true, true)]
    public async Task Placa_y_chasis_admiten_identificadores_alfanumericos_y_el_chasis_sigue_siendo_opcional(
        string? valor, bool placaValida, bool chasisValido)
    {
        foreach (var chasis in new[] { true, false })
        foreach (var nuevo in new[] { true, false })
        {
            using var db = FabricaDeContextos.EnMemoria();
            FabricaDeContextos.SembrarBase(db);
            var valido = chasis ? chasisValido : placaValida;
            var entrada = await db.Vehiculos.AsNoTracking().SingleAsync(v => v.IdVehiculo == 1);
            entrada.IdVehiculo = nuevo ? 0 : 1;
            if (chasis) { entrada.Placa = "M NUEVA9"; entrada.NumChasis = valor; }
            else entrada.Placa = valor!;
            ComprobarFormulario(new VehiculoFormViewModel
            {
                IdCliente = 1, Placa = entrada.Placa, NumChasis = entrada.NumChasis,
                Marca = "Citroën", Modelo = "C4", Anio = 2020
            }, valido, chasis ? "NumChasis" : "Placa");
            var vehiculos = new VehiculoService(db);
            var resultado = nuevo ? await vehiculos.Crear(entrada) : await vehiculos.Editar(entrada);
            Assert.Equal(valido, resultado.EsCorrecto);
            if (!valido)
            {
                Assert.Equal(3, await db.Vehiculos.CountAsync());
                var guardado = (await vehiculos.ObtenerPorId(1))!;
                Assert.Equal("MGA-1234", guardado.Placa);
                Assert.Null(guardado.NumChasis);
            }
        }
    }

    [Theory]
    [InlineData("mga1234", false)]
    [InlineData(" M G A-1 2 3 4 ", false)]
    [InlineData("abcd123", true)]
    [InlineData(" AB-CD 123 ", true)]
    public async Task Separadores_y_mayusculas_no_permiten_duplicar_placa_o_chasis_incluso_inactivos(string valor, bool chasis)
    {
        foreach (var activo in new[] { true, false })
        {
            using var db = FabricaDeContextos.EnMemoria();
            FabricaDeContextos.SembrarBase(db);
            var titular = (await db.Vehiculos.FindAsync(1))!;
            titular.NumChasis = " ab cd-123 ";
            titular.Activo = activo;
            await db.SaveChangesAsync();
            var vehiculos = new VehiculoService(db);
            var entrada = new Vehiculo { IdCliente = 1, Placa = chasis ? "M NUEVA9" : valor, NumChasis = chasis ? valor : null, Marca = "Toyota", Modelo = "Corolla", Anio = 2020 };
            Assert.False((await vehiculos.Crear(entrada)).EsCorrecto);
            entrada.IdVehiculo = 2;
            Assert.False((await vehiculos.Editar(entrada)).EsCorrecto);
            Assert.Equal("MGB-5678", (await vehiculos.ObtenerPorId(2))!.Placa);
            var propia = (await vehiculos.ObtenerPorId(1))!;
            if (chasis) propia.NumChasis = valor; else propia.Placa = valor;
            Assert.True((await vehiculos.Editar(propia)).EsCorrecto);
            Assert.Equal(activo, (await vehiculos.ObtenerPorId(1))!.Activo);
            Assert.Equal(3, await db.Vehiculos.CountAsync());
        }
    }

    [Fact]
    public async Task Anio_del_formulario_y_del_servicio_comparten_limites_actuales()
    {
        foreach (var anio in new[] { 1949, 1950, DateTime.Now.Year + 1, DateTime.Now.Year + 2 })
        {
            var valido = anio is not 1949 && anio != DateTime.Now.Year + 2;
            using var db = FabricaDeContextos.EnMemoria();
            FabricaDeContextos.SembrarBase(db);
            ComprobarFormulario(new VehiculoFormViewModel { IdCliente = 1, Placa = "M NUEVA9", Marca = "Toyota", Modelo = "Corolla", Anio = anio }, valido, "Anio");
            var entrada = new Vehiculo { IdCliente = 1, Placa = "M NUEVA9", Marca = "Toyota", Modelo = "Corolla", Anio = anio };
            var vehiculos = new VehiculoService(db);
            Assert.Equal(valido, (await vehiculos.Crear(entrada)).EsCorrecto);
            entrada.IdVehiculo = 1;
            entrada.Placa = "MGA-1234";
            Assert.Equal(valido, (await vehiculos.Editar(entrada)).EsCorrecto);
            Assert.Equal(valido ? anio : 2018, (await vehiculos.ObtenerPorId(1))!.Anio);
        }
    }

    [Theory]
    [InlineData("PrecioUnitario", "10.001", false)]
    [InlineData("Costo", "10.001", false)]
    [InlineData("Stock", "10.0001", false)]
    [InlineData("PrecioUnitario", "10.01", true)]
    [InlineData("Costo", "10.01", true)]
    [InlineData("Stock", "10.001", true)]
    public async Task Precision_de_producto_se_valida_por_campo_y_no_se_redondean_entradas_invalidas(string campo, string texto, bool valido)
    {
        var valor = decimal.Parse(texto, CultureInfo.InvariantCulture);
        foreach (var nuevo in new[] { true, false })
        {
            using var db = FabricaDeContextos.EnMemoria();
            FabricaDeContextos.SembrarBase(db);
            var entrada = await db.Productos.AsNoTracking().SingleAsync(p => p.IdProducto == 1);
            var stockOriginal = entrada.Stock;
            entrada.IdProducto = nuevo ? 0 : 1;
            if (campo == "PrecioUnitario") entrada.PrecioUnitario = valor;
            if (campo == "Costo") entrada.Costo = valor;
            if (campo == "Stock") entrada.Stock = valor;
            ComprobarFormulario(new ProductoFormViewModel
            {
                Nombre = entrada.Nombre, Descripcion = entrada.Descripcion, PrecioUnitario = entrada.PrecioUnitario,
                Costo = entrada.Costo, Stock = entrada.Stock
            }, valido, campo);
            var productos = new ProductoService(db);
            Assert.Equal(valido, (nuevo ? await productos.Crear(entrada) : await productos.Editar(entrada, stockOriginal)).EsCorrecto);
            if (!valido)
            {
                Assert.Equal(2, await db.Productos.CountAsync());
                var conservado = (await productos.ObtenerPorId(1))!;
                Assert.Equal(350m, conservado.PrecioUnitario);
                Assert.Equal(0m, conservado.Costo);
                Assert.Equal(20m, conservado.Stock);
            }
        }
    }

    [Fact]
    public async Task Factura_exige_servicios_positivos_no_vacios_y_sin_repeticiones()
    {
        foreach (var ids in new List<int>[] { [], [0], [-1], [1, 1] })
        {
            using var db = FabricaDeContextos.EnMemoria();
            FabricaDeContextos.SembrarBase(db);
            var servicio = FabricaDeContextos.NuevoServicio(db, 1, "Mantenimiento", 100m, estado: "Finalizado");
            ComprobarFormulario(new FacturaCrearViewModel { IdCliente = 1, MetodoPago = "Efectivo", IdServicios = ids }, false, "IdServicios");
            var facturas = new FacturacionService(db, FabricaDeContextos.Configuracion());
            Assert.False((await facturas.CrearFactura(1, "Efectivo", ids, null)).EsCorrecto);
            Assert.Empty(await db.Facturas.ToListAsync());
            Assert.Empty(await db.DetalleFactura.ToListAsync());
            Assert.True((await facturas.CrearFactura(1, "Efectivo", [servicio.IdServicio], null)).EsCorrecto);
            Assert.Single(await db.DetalleFactura.ToListAsync());
        }
    }

    [Fact]
    public void Selecciones_y_precision_se_rechazan_antes_de_llamar_al_servicio()
    {
        ComprobarFormulario(new VehiculoFormViewModel { IdCliente = 0, Placa = "M NUEVA9", Marca = "Toyota", Modelo = "Corolla", Anio = 2020 }, false, "IdCliente");
        ComprobarFormulario(new TrabajadorFormViewModel { Nombre = "Ana", Apellido = "López", Telefono = "81234567", Area = "Otra" }, false, "Area");
        var servicio = new ServicioFormViewModel { IdVehiculo = 1, IdTrabajadorInicial = 1, TipoServicio = "Otro", Descripcion = "Prueba" };
        ComprobarFormulario(servicio, false, "TipoServicio");
        servicio.TipoServicio = "Mantenimiento";
        servicio.DescripcionFalla = "Ruido";
        servicio.Total = 0.001m;
        ComprobarFormulario(servicio, false, "Total");
        ComprobarFormulario(new ProductoUtilizadoFormViewModel { IdServicio = 1, IdProducto = 1, Cantidad = 0.0001m }, false, "Cantidad");
        ComprobarFormulario(new AsignarTrabajadorViewModel { IdServicio = 1, IdTrabajador = 0 }, false, "IdTrabajador");
        ComprobarFormulario(new FacturaCrearViewModel { IdCliente = 1, MetodoPago = "Cheque", IdServicios = [1] }, false, "MetodoPago");
    }

    private static void ComprobarFormulario(object formulario, bool valido, string campo)
    {
        var errores = new List<ValidationResult>();
        Assert.Equal(valido, Validator.TryValidateObject(formulario, new ValidationContext(formulario), errores, true));
        if (!valido) Assert.Contains(errores, e => e.MemberNames.Contains(campo));
    }
}
