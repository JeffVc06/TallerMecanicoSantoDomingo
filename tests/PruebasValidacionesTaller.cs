using Microsoft.EntityFrameworkCore;
using TallerMecanicoSantoDomingo.Models;
using TallerMecanicoSantoDomingo.Services;
using Xunit;

namespace TallerMecanicoSantoDomingo.Tests;

public class PruebasValidacionesTaller
{
    private static Servicio Entrada(string tipo = "Mantenimiento") => new()
    {
        IdVehiculo = 1, TipoServicio = tipo, Descripcion = "Revisión del vehículo",
        DescripcionFalla = tipo == "Mantenimiento" ? "Ruido al frenar" : null,
        TipoPintura = tipo == "Pintura" ? "Base de agua" : null,
        Color = tipo == "Pintura" ? "Rojo" : null,
        FechaInicio = new DateOnly(2026, 9, 1), Total = 1000m
    };

    [Fact]
    public async Task Abrir_exige_personal_y_guarda_servicio_y_asignacion_juntos()
    {
        using var db = FabricaDeContextos.EnMemoria();
        FabricaDeContextos.SembrarBase(db);
        var servicios = new ServicioService(db);
        Assert.False((await servicios.Crear(Entrada())).EsCorrecto);
        Assert.Empty(await db.Servicios.ToListAsync());
        Assert.True((await servicios.Crear(Entrada(), 1)).EsCorrecto);
        Assert.True((await servicios.Crear(Entrada(), 1)).EsCorrecto);
        var tercera = await servicios.Crear(Entrada(), 1);
        Assert.False(tercera.EsCorrecto);
        Assert.Contains("dos servicios activos", tercera.Mensaje);
        Assert.Equal(2, await db.Servicios.CountAsync());
        Assert.Equal(2, await db.DetalleServicio.CountAsync());

        var primera = (await servicios.ObtenerPorId((await db.Servicios.FirstAsync()).IdServicio))!;
        primera.Descripcion = "Revisión actualizada";
        Assert.True((await servicios.Editar(primera)).EsCorrecto); // No cuenta la edición como un tercer trabajo.
    }

    [Theory]
    [InlineData(false, false, "Mantenimiento")]
    [InlineData(true, true, "Mantenimiento")]
    [InlineData(true, false, "Pintura")]
    public async Task Trabajador_inactivo_con_salida_o_de_otra_area_no_recibe_trabajos(bool activo, bool salida, string tipo)
    {
        using var db = FabricaDeContextos.EnMemoria();
        FabricaDeContextos.SembrarBase(db);
        var trabajador = (await db.Trabajadores.FindAsync(1))!;
        trabajador.Activo = activo;
        trabajador.FechaSalida = salida ? new DateOnly(2026, 10, 1) : null;
        await db.SaveChangesAsync();
        Assert.False((await new ServicioService(db).Crear(Entrada(tipo), 1)).EsCorrecto);
        var servicio = FabricaDeContextos.NuevoServicio(db, 1, tipo, 100);
        Assert.False((await new AsignacionService(db).Asignar(servicio.IdServicio, 1, null)).EsCorrecto);
        Assert.Empty(await db.DetalleServicio.ToListAsync());
    }

    [Theory]
    [InlineData("Mantenimiento", null, null, null)]
    [InlineData("Mantenimiento", " ", null, null)]
    [InlineData("Pintura", null, " ", "Rojo")]
    [InlineData("Pintura", null, "Base de agua", " ")]
    public async Task Campos_especificos_se_exigen_en_el_servidor(string tipo, string? falla, string? pintura, string? color)
    {
        using var db = FabricaDeContextos.EnMemoria();
        FabricaDeContextos.SembrarBase(db);
        var entrada = Entrada(tipo);
        entrada.DescripcionFalla = falla; entrada.TipoPintura = pintura; entrada.Color = color;
        Assert.False((await new ServicioService(db).Crear(entrada, tipo == "Pintura" ? 2 : 1)).EsCorrecto);
        Assert.Empty(await db.Servicios.ToListAsync());
    }

    [Fact]
    public async Task Retirar_ultimo_se_rechaza_y_reactivar_reutiliza_la_pareja()
    {
        using var db = FabricaDeContextos.EnMemoria();
        FabricaDeContextos.SembrarBase(db);
        db.Trabajadores.Add(new Trabajador { IdTrabajador = 3, Nombre = "José Ángel", Apellido = "De la Cruz", Telefono = "8888-9999" });
        await db.SaveChangesAsync();
        var servicio = await new ServicioService(db).Crear(Entrada(), 1);
        var id = servicio.Datos!.IdServicio;
        var asignaciones = new AsignacionService(db);
        Assert.False((await asignaciones.Desasignar(id, 1)).EsCorrecto);
        Assert.True((await asignaciones.Asignar(id, 3, "Relevo")).EsCorrecto);
        Assert.True((await asignaciones.Desasignar(id, 1)).EsCorrecto);
        Assert.True((await asignaciones.Asignar(id, 1, null)).EsCorrecto);
        Assert.False((await asignaciones.Asignar(id, 1, null)).EsCorrecto);
        Assert.Equal(2, await db.DetalleServicio.CountAsync());
    }

    [Fact]
    public async Task No_se_cambia_area_salida_o_estado_con_trabajos_activos_pero_el_historial_no_bloquea()
    {
        using var db = FabricaDeContextos.EnMemoria();
        FabricaDeContextos.SembrarBase(db);
        var servicio = await new ServicioService(db).Crear(Entrada(), 1);
        var trabajadores = new TrabajadorService(db);
        var entrada = await db.Trabajadores.AsNoTracking().SingleAsync(t => t.IdTrabajador == 1);
        entrada.Area = "Pintura";
        Assert.False((await trabajadores.Guardar(entrada, false)).EsCorrecto);
        entrada.Area = "Mantenimiento mecánico"; entrada.FechaSalida = new DateOnly(2026, 12, 1);
        Assert.False((await trabajadores.Guardar(entrada, false)).EsCorrecto);
        Assert.False((await trabajadores.CambiarEstado(1, false)).EsCorrecto);
        Assert.True((await new ServicioService(db).Finalizar(servicio.Datos!.IdServicio, new DateOnly(2026, 9, 2))).EsCorrecto);
        entrada.FechaSalida = null; entrada.Area = "Pintura";
        Assert.True((await trabajadores.Guardar(entrada, false)).EsCorrecto);
        Assert.Equal("Retirado", (await db.DetalleServicio.SingleAsync()).Estado);
    }

    [Fact]
    public async Task Correo_opcional_es_unico_y_una_edicion_propia_no_es_duplicada()
    {
        using var db = FabricaDeContextos.EnMemoria();
        FabricaDeContextos.SembrarBase(db);
        var trabajadores = new TrabajadorService(db);
        var primero = await db.Trabajadores.AsNoTracking().SingleAsync(t => t.IdTrabajador == 1);
        primero.Correo = " mecanico@taller.example ";
        Assert.True((await trabajadores.Guardar(primero, false)).EsCorrecto);
        Assert.True((await trabajadores.Guardar(primero, false)).EsCorrecto);
        var segundo = await db.Trabajadores.AsNoTracking().SingleAsync(t => t.IdTrabajador == 2);
        segundo.Correo = "MECANICO@TALLER.EXAMPLE";
        Assert.False((await trabajadores.Guardar(segundo, false)).EsCorrecto);
        segundo.Correo = " ";
        Assert.True((await trabajadores.Guardar(segundo, false)).EsCorrecto);
        Assert.Null((await db.Trabajadores.AsNoTracking().SingleAsync(t => t.IdTrabajador == 2)).Correo);
    }

    [Fact]
    public async Task Fechas_no_admiten_ausencia_inversion_ni_estado_manual()
    {
        using var db = FabricaDeContextos.EnMemoria();
        FabricaDeContextos.SembrarBase(db);
        var servicios = new ServicioService(db);
        var entrada = Entrada(); entrada.Estado = "Cualquiera";
        Assert.False((await servicios.Crear(entrada, 1)).EsCorrecto);
        entrada.Estado = "Abierto"; entrada.FechaInicio = default;
        Assert.False((await servicios.Crear(entrada, 1)).EsCorrecto);
        var creado = await servicios.Crear(Entrada(), 1);
        var id = creado.Datos!.IdServicio;
        Assert.False((await servicios.Finalizar(id, default)).EsCorrecto);
        Assert.False((await servicios.Finalizar(id, new DateOnly(2026, 8, 31))).EsCorrecto);
        Assert.True((await servicios.Finalizar(id, new DateOnly(2026, 9, 2))).EsCorrecto);
        Assert.False((await servicios.Cancelar(id, null)).EsCorrecto);
    }

    [Fact]
    public async Task Consumo_decimal_exacto_limite_y_reintentos_no_descontados_dos_veces()
    {
        using var db = FabricaDeContextos.EnMemoria();
        FabricaDeContextos.SembrarBase(db);
        var producto = (await db.Productos.FindAsync(1))!; producto.Stock = 1.25m;
        await db.SaveChangesAsync();
        var s1 = FabricaDeContextos.NuevoServicio(db, 1, "Mantenimiento", 100);
        var s2 = FabricaDeContextos.NuevoServicio(db, 2, "Mantenimiento", 100);
        var productos = new ProductoService(db);
        Assert.True((await productos.AgregarAlServicio(s1.IdServicio, 1, 1.25m)).EsCorrecto);
        Assert.Equal(0m, (await productos.ObtenerPorId(1))!.Stock);
        Assert.False((await productos.AgregarAlServicio(s1.IdServicio, 1, 1.25m)).EsCorrecto);
        Assert.False((await productos.AgregarAlServicio(s2.IdServicio, 1, 0.001m)).EsCorrecto);
        Assert.True((await productos.ActualizarCantidad(s1.IdServicio, 1, 0.25m)).EsCorrecto);
        Assert.True((await productos.ActualizarCantidad(s1.IdServicio, 1, 0.25m)).EsCorrecto);
        Assert.Equal(1m, (await productos.ObtenerPorId(1))!.Stock);
        Assert.True((await productos.QuitarDelServicio(s1.IdServicio, 1)).EsCorrecto);
        Assert.False((await productos.QuitarDelServicio(s1.IdServicio, 1)).EsCorrecto);
        Assert.Equal(1.25m, (await productos.ObtenerPorId(1))!.Stock);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-0.001")]
    [InlineData("0.0001")]
    [InlineData("1000000000")]
    public async Task Consumos_rechazan_cantidades_fuera_de_precision(string valor)
    {
        using var db = FabricaDeContextos.EnMemoria();
        FabricaDeContextos.SembrarBase(db);
        var servicio = FabricaDeContextos.NuevoServicio(db, 1, "Mantenimiento", 100);
        var cantidad = decimal.Parse(valor, System.Globalization.CultureInfo.InvariantCulture);
        Assert.False((await new ProductoService(db).AgregarAlServicio(servicio.IdServicio, 1, cantidad)).EsCorrecto);
        Assert.Empty(await db.ProductosUtilizados.ToListAsync());
        Assert.Equal(20m, (await db.Productos.AsNoTracking().SingleAsync(p => p.IdProducto == 1)).Stock);
    }

    [Fact]
    public async Task Inactivo_no_admite_consumos_ni_aumentos_pero_si_correccion_explicita()
    {
        using var db = FabricaDeContextos.EnMemoria();
        FabricaDeContextos.SembrarBase(db);
        var servicio = FabricaDeContextos.NuevoServicio(db, 1, "Mantenimiento", 100);
        var productos = new ProductoService(db);
        Assert.True((await productos.AgregarAlServicio(servicio.IdServicio, 1, 2)).EsCorrecto);
        Assert.True((await productos.CambiarEstado(1, false)).EsCorrecto);
        Assert.False((await productos.ActualizarCantidad(servicio.IdServicio, 1, 3)).EsCorrecto);
        Assert.True((await productos.ActualizarCantidad(servicio.IdServicio, 1, 1)).EsCorrecto);
        Assert.Equal(19m, (await productos.ObtenerPorId(1))!.Stock);
    }

    [Fact]
    public async Task Placa_edicion_propia_longitudes_y_transferencia_con_historial()
    {
        using var db = FabricaDeContextos.EnMemoria();
        FabricaDeContextos.SembrarBase(db);
        var vehiculos = new VehiculoService(db);
        var vehiculo = (await vehiculos.ObtenerPorId(1))!;
        Assert.True((await vehiculos.Editar(vehiculo)).EsCorrecto);
        var duplicado = new Vehiculo { IdCliente = 1, Placa = " mga-1234 ", Marca = "Citroën", Modelo = "C4", Anio = 2020 };
        Assert.False((await vehiculos.Crear(duplicado)).EsCorrecto);
        duplicado.Placa = new string('M', 21);
        Assert.False((await vehiculos.Crear(duplicado)).EsCorrecto);
        FabricaDeContextos.NuevoServicio(db, 1, "Mantenimiento", 100);
        vehiculo.IdCliente = 2;
        Assert.False((await vehiculos.Editar(vehiculo)).EsCorrecto);
        Assert.Equal(1, (await vehiculos.ObtenerPorId(1))!.IdCliente);
    }

    [Fact]
    public async Task Factura_exige_finalizados_y_no_deja_cabecera_si_fallan_cliente_o_importes()
    {
        using var db = FabricaDeContextos.EnMemoria();
        FabricaDeContextos.SembrarBase(db);
        var s1 = FabricaDeContextos.NuevoServicio(db, 1, "Mantenimiento", 100);
        var s2 = FabricaDeContextos.NuevoServicio(db, 3, "Mantenimiento", 100, estado: "Finalizado");
        var facturas = new FacturacionService(db, FabricaDeContextos.Configuracion());
        Assert.False((await facturas.CrearFactura(1, "Efectivo", [], null)).EsCorrecto);
        Assert.False((await facturas.CrearFactura(1, "Efectivo", [s1.IdServicio], null)).EsCorrecto);
        Assert.False((await facturas.CrearFactura(1, "Efectivo", [s2.IdServicio], null)).EsCorrecto);
        Assert.Empty(await db.Facturas.ToListAsync());
        Assert.Empty(await db.DetalleFactura.ToListAsync());
        var enorme = FabricaDeContextos.NuevoServicio(db, 1, "Mantenimiento", 9999999999.99m, estado: "Finalizado");
        Assert.False((await facturas.CrearFactura(1, "Efectivo", [enorme.IdServicio], null)).EsCorrecto);
        Assert.Empty(await db.Facturas.ToListAsync());
    }

    [Fact]
    public async Task Factura_congela_importes_ruc_y_no_repone_inventario_al_anular()
    {
        using var db = FabricaDeContextos.EnMemoria();
        FabricaDeContextos.SembrarBase(db);
        var servicio = (await new ServicioService(db).Crear(Entrada(), 1)).Datos!;
        var productos = new ProductoService(db);
        Assert.True((await productos.AgregarAlServicio(servicio.IdServicio, 1, 2)).EsCorrecto);
        Assert.True((await new ServicioService(db).Finalizar(servicio.IdServicio, new DateOnly(2026, 9, 2))).EsCorrecto);
        var facturas = new FacturacionService(db, FabricaDeContextos.Configuracion());
        var factura = (await facturas.CrearFactura(1, "Efectivo", [servicio.IdServicio], null)).Datos!;
        Assert.False((await productos.ActualizarCantidad(servicio.IdServicio, 1, 3)).EsCorrecto);
        Assert.False((await new ServicioService(db).Editar(servicio)).EsCorrecto);
        var producto = (await productos.ObtenerPorId(1))!;
        producto.PrecioUnitario = 5000;
        Assert.True((await productos.Editar(producto, producto.Stock)).EsCorrecto);
        var cliente = (await new ClienteService(db).ObtenerPorId(1))!;
        cliente.RFC = "J0310000000099";
        Assert.True((await new ClienteService(db).Editar(cliente)).EsCorrecto);
        var guardada = (await facturas.ObtenerParaImpresion(factura.IdFactura))!;
        Assert.Equal("RFC-0001", guardada.Factura.RUC);
        Assert.Equal(1000m, Assert.Single(guardada.Detalles).Subtotal);
        Assert.Equal(1150m, guardada.Factura.Total);
        Assert.True((await facturas.AnularFactura(factura.IdFactura, "Corrección autorizada")).EsCorrecto);
        Assert.Equal(18m, (await productos.ObtenerPorId(1))!.Stock);
        Assert.Single(await db.ProductosUtilizados.ToListAsync());
        Assert.Single(await db.DetalleFactura.ToListAsync());
        var nueva = await facturas.CrearFactura(1, "Efectivo", [servicio.IdServicio], null);
        Assert.True(nueva.EsCorrecto);
        Assert.NotEqual(factura.NumeroFactura, nueva.Datos!.NumeroFactura);
        Assert.Equal(2, await db.DetalleFactura.CountAsync());
    }

    [Fact]
    public async Task Asignaciones_historicas_activas_en_servicios_terminados_no_ocupan_capacidad()
    {
        using var db = FabricaDeContextos.EnMemoria();
        FabricaDeContextos.SembrarBase(db);
        var historico = FabricaDeContextos.NuevoServicio(db, 1, "Mantenimiento", 100, estado: "Finalizado");
        db.DetalleServicio.Add(new DetalleServicio { IdServicio = historico.IdServicio, IdTrabajador = 1, Estado = "Activo" });
        await db.SaveChangesAsync();
        var servicios = new ServicioService(db);
        Assert.True((await servicios.Crear(Entrada(), 1)).EsCorrecto);
        Assert.True((await servicios.Crear(Entrada(), 1)).EsCorrecto);
        Assert.False((await servicios.Crear(Entrada(), 1)).EsCorrecto);
        var carga = Assert.Single(await servicios.CargasDeTrabajadores([1]));
        Assert.Equal(2, carga.ServiciosActivos);
        // Se detecta por su relación con el estado del servicio, sin reparar el dato histórico.
        Assert.Equal("Activo", (await db.DetalleServicio.AsNoTracking().SingleAsync(d => d.IdServicio == historico.IdServicio)).Estado);
    }

    [Fact]
    public async Task Aumentar_consumo_descuenta_solo_diferencia_y_no_guarda_un_exceso()
    {
        using var db = FabricaDeContextos.EnMemoria();
        FabricaDeContextos.SembrarBase(db);
        var servicio = FabricaDeContextos.NuevoServicio(db, 1, "Mantenimiento", 100);
        var productos = new ProductoService(db);
        Assert.True((await productos.AgregarAlServicio(servicio.IdServicio, 1, 2)).EsCorrecto);
        Assert.True((await productos.ActualizarCantidad(servicio.IdServicio, 1, 5)).EsCorrecto);
        Assert.True((await productos.ActualizarCantidad(servicio.IdServicio, 1, 5)).EsCorrecto);
        Assert.Equal(15m, (await productos.ObtenerPorId(1))!.Stock);
        Assert.False((await productos.ActualizarCantidad(servicio.IdServicio, 1, 21)).EsCorrecto);
        Assert.Equal(15m, (await productos.ObtenerPorId(1))!.Stock);
        Assert.Equal(5m, (await db.ProductosUtilizados.AsNoTracking().SingleAsync()).Cantidad);
    }

    [Fact]
    public async Task Editar_catalogo_no_sobrescribe_stock_consumido_desde_un_formulario_antiguo()
    {
        using var db = FabricaDeContextos.EnMemoria();
        FabricaDeContextos.SembrarBase(db);
        var productos = new ProductoService(db);
        var formulario = (await productos.ObtenerPorId(1))!;
        var saldoOriginal = formulario.Stock;
        var servicio = FabricaDeContextos.NuevoServicio(db, 1, "Mantenimiento", 100);
        Assert.True((await productos.AgregarAlServicio(servicio.IdServicio, 1, 2)).EsCorrecto);
        formulario.PrecioUnitario = 500;
        Assert.False((await productos.Editar(formulario, saldoOriginal)).EsCorrecto);
        Assert.False((await productos.Editar(formulario, null)).EsCorrecto);
        Assert.Equal(18m, (await productos.ObtenerPorId(1))!.Stock);
        var actualizado = (await productos.ObtenerPorId(1))!;
        actualizado.PrecioUnitario = 500;
        Assert.True((await productos.Editar(actualizado, actualizado.Stock)).EsCorrecto);
        Assert.Equal(18m, (await productos.ObtenerPorId(1))!.Stock);
    }

    [Fact]
    public async Task Reactivar_trabajador_no_acepta_sobrecarga_historica_sin_resolverla()
    {
        using var db = FabricaDeContextos.EnMemoria();
        FabricaDeContextos.SembrarBase(db);
        (await db.Trabajadores.FindAsync(1))!.Activo = false;
        await db.SaveChangesAsync();
        var ids = Enumerable.Range(0, 3).Select(_ => FabricaDeContextos.NuevoServicio(db, 1, "Mantenimiento", 100).IdServicio).ToArray();
        db.DetalleServicio.AddRange(ids.Select(id => new DetalleServicio { IdServicio = id, IdTrabajador = 1, Estado = "Activo" }));
        await db.SaveChangesAsync();
        var trabajadores = new TrabajadorService(db);
        Assert.False((await trabajadores.CambiarEstado(1, true)).EsCorrecto);
        Assert.False((await db.Trabajadores.AsNoTracking().SingleAsync(t => t.IdTrabajador == 1)).Activo);
        Assert.True((await new ServicioService(db).Cancelar(ids[0], null)).EsCorrecto);
        Assert.True((await trabajadores.CambiarEstado(1, true)).EsCorrecto);
    }

    [Fact]
    public async Task Numeracion_sigue_el_valor_numerico_despues_de_9999()
    {
        using var db = FabricaDeContextos.EnMemoria();
        FabricaDeContextos.SembrarBase(db);
        foreach (var numero in new[] { 9999, 10000 })
            db.Facturas.Add(new Factura { IdCliente = 1, NumeroFactura = $"FAC-{DateTime.Now.Year}-{numero}", Estado = "Anulada" });
        await db.SaveChangesAsync();
        var servicio = FabricaDeContextos.NuevoServicio(db, 1, "Mantenimiento", 100, estado: "Finalizado");
        var resultado = await new FacturacionService(db, FabricaDeContextos.Configuracion()).CrearFactura(1, "Efectivo", [servicio.IdServicio], null);
        Assert.True(resultado.EsCorrecto);
        Assert.EndsWith("-10001", resultado.Datos!.NumeroFactura);
    }
}
