using Microsoft.EntityFrameworkCore;
using TallerMecanicoSantoDomingo.Models;
using TallerMecanicoSantoDomingo.Services;
using Xunit;

namespace TallerMecanicoSantoDomingo.Tests;

/// <summary>Reglas de negocio probadas con EF Core en memoria.</summary>
public class PruebasUnitariasReglas
{
    // Regla 1: identificadores únicos (RFC de cliente, placa de vehículo, número de factura)

    [Fact]
    public async Task Regla1_RFC_de_cliente_duplicado_es_rechazado()
    {
        using var db = FabricaDeContextos.EnMemoria();
        var servicio = new ClienteService(db);

        var primero = await servicio.Crear(new Cliente
        {
            Nombre = "Juan", Apellido = "Pérez", Telefono = "8888-1111",
            Direccion = "Managua", RFC = "RFC-0001"
        });
        Assert.True(primero.EsCorrecto);

        var duplicado = await servicio.Crear(new Cliente
        {
            Nombre = "Otro", Apellido = "Cliente", Telefono = "8999-9999",
            Direccion = "León", RFC = "rfc-0001"
        });

        Assert.False(duplicado.EsCorrecto);
        Assert.Contains("RFC", duplicado.Mensaje);
        Assert.Equal(1, await db.Clientes.CountAsync());
    }

    [Fact]
    public async Task Regla1_RFC_se_guarda_en_mayusculas_sin_espacios()
    {
        using var db = FabricaDeContextos.EnMemoria();
        var servicio = new ClienteService(db);

        await servicio.Crear(new Cliente
        {
            Nombre = "  Juan ", Apellido = " Pérez ", Telefono = " 8888 ",
            Direccion = " Managua ", RFC = " rfc-abc "
        });

        var cliente = await db.Clientes.SingleAsync();
        Assert.Equal("RFC-ABC", cliente.RFC);
        Assert.Equal("Juan", cliente.Nombre);
    }

    [Fact]
    public async Task Regla1_Placa_de_vehiculo_duplicada_es_rechazada()
    {
        using var db = FabricaDeContextos.EnMemoria();
        FabricaDeContextos.SembrarBase(db);
        var servicio = new VehiculoService(db);

        var resultado = await servicio.Crear(new Vehiculo
        {
            IdCliente = 1, Placa = "mga-1234", Marca = "Toyota", Modelo = "Corolla", Anio = 2020
        });

        Assert.False(resultado.EsCorrecto);
        Assert.Contains("placa", resultado.Mensaje, StringComparison.OrdinalIgnoreCase);
    }
    // Regla 2: un vehículo pertenece a un cliente existente (1:N)

    [Fact]
    public async Task Regla2_Vehiculo_con_cliente_inexistente_es_rechazado()
    {
        using var db = FabricaDeContextos.EnMemoria();
        FabricaDeContextos.SembrarBase(db);
        var servicio = new VehiculoService(db);

        var resultado = await servicio.Crear(new Vehiculo
        {
            IdCliente = 999, Placa = "MZZ-0001", Marca = "Kia", Modelo = "Rio", Anio = 2021
        });

        Assert.False(resultado.EsCorrecto);
        Assert.Contains("cliente", resultado.Mensaje, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Regla2_Un_cliente_puede_tener_varios_vehiculos()
    {
        using var db = FabricaDeContextos.EnMemoria();
        FabricaDeContextos.SembrarBase(db);
        var servicio = new ClienteService(db);

        var vehiculos = await servicio.ObtenerVehiculosDelCliente(1);

        Assert.Equal(2, vehiculos.Count);
        Assert.All(vehiculos, v => Assert.Equal(1, v.IdCliente));
    }
    // Reglas 3 y 4: cada servicio pertenece a un vehículo; el tipo solo puede ser
    // Mantenimiento o Pintura

    [Fact]
    public async Task Regla3_Servicio_con_vehiculo_inexistente_es_rechazado()
    {
        using var db = FabricaDeContextos.EnMemoria();
        FabricaDeContextos.SembrarBase(db);
        var servicio = new ServicioService(db);

        var resultado = await servicio.Crear(new Servicio
        {
            IdVehiculo = 999, TipoServicio = "Mantenimiento",
            Descripcion = "Prueba", FechaInicio = new DateOnly(2026, 9, 1), Total = 100m
        });

        Assert.False(resultado.EsCorrecto);
        Assert.Contains("vehículo", resultado.Mensaje, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Regla4_Tipo_de_servicio_invalido_es_rechazado()
    {
        using var db = FabricaDeContextos.EnMemoria();
        FabricaDeContextos.SembrarBase(db);
        var servicio = new ServicioService(db);

        var resultado = await servicio.Crear(new Servicio
        {
            IdVehiculo = 1, TipoServicio = "Electricidad",
            Descripcion = "Prueba", FechaInicio = new DateOnly(2026, 9, 1), Total = 100m
        });

        Assert.False(resultado.EsCorrecto);
        Assert.Contains("tipo de servicio", resultado.Mensaje, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Regla4_Servicio_valido_queda_abierto()
    {
        using var db = FabricaDeContextos.EnMemoria();
        FabricaDeContextos.SembrarBase(db);
        var servicio = new ServicioService(db);

        var resultado = await servicio.Crear(new Servicio
        {
            IdVehiculo = 1, TipoServicio = "Mantenimiento",
            Descripcion = "Cambio de aceite", FechaInicio = new DateOnly(2026, 9, 1), Total = 1500m
        });

        Assert.True(resultado.EsCorrecto);
        Assert.Equal("Abierto", resultado.Datos!.Estado);
        Assert.Null(resultado.Datos.FechaFin);
    }
    // Regla 5: el área del trabajador debe ser compatible con el tipo de servicio

    [Fact]
    public async Task Regla5_Asignar_pintor_a_servicio_de_mecanica_es_rechazado()
    {
        using var db = FabricaDeContextos.EnMemoria();
        FabricaDeContextos.SembrarBase(db);
        var mecanica = FabricaDeContextos.NuevoServicio(db, 1, "Mantenimiento", 1000m);
        var asignaciones = new AsignacionService(db);

        // Trabajador 2 pertenece al área de Pintura.
        var resultado = await asignaciones.Asignar(mecanica.IdServicio, 2, null);

        Assert.False(resultado.EsCorrecto);
        Assert.Contains("no puede recibir servicios de tipo", resultado.Mensaje);
        Assert.Empty(await db.DetalleServicio.ToListAsync());
    }
    // Regla 6: la pareja servicio–trabajador no se repite

    [Fact]
    public async Task Regla6_No_se_repite_la_pareja_servicio_trabajador()
    {
        using var db = FabricaDeContextos.EnMemoria();
        FabricaDeContextos.SembrarBase(db);
        var mecanica = FabricaDeContextos.NuevoServicio(db, 1, "Mantenimiento", 1000m);
        var asignaciones = new AsignacionService(db);

        var primera = await asignaciones.Asignar(mecanica.IdServicio, 1, null);
        var repetida = await asignaciones.Asignar(mecanica.IdServicio, 1, null);

        Assert.True(primera.EsCorrecto);
        Assert.False(repetida.EsCorrecto);
        Assert.Contains("ya está asignado", repetida.Mensaje);
        Assert.Equal(1, await db.DetalleServicio.CountAsync());
    }
    // Regla 7: máximo dos servicios activos por trabajador

    [Fact]
    public async Task Regla7_Un_tercer_servicio_activo_es_rechazado()
    {
        using var db = FabricaDeContextos.EnMemoria();
        FabricaDeContextos.SembrarBase(db);
        var asignaciones = new AsignacionService(db);

        var s1 = FabricaDeContextos.NuevoServicio(db, 1, "Mantenimiento", 100m);
        var s2 = FabricaDeContextos.NuevoServicio(db, 2, "Mantenimiento", 200m);
        var s3 = FabricaDeContextos.NuevoServicio(db, 3, "Mantenimiento", 300m);

        Assert.True((await asignaciones.Asignar(s1.IdServicio, 1, null)).EsCorrecto);
        Assert.True((await asignaciones.Asignar(s2.IdServicio, 1, null)).EsCorrecto);

        var tercero = await asignaciones.Asignar(s3.IdServicio, 1, null);

        Assert.False(tercero.EsCorrecto);
        Assert.Contains("máximo permitido es 2", tercero.Mensaje);
        Assert.Equal(2, await db.DetalleServicio.CountAsync());
    }

    [Fact]
    public async Task Regla7_Al_retirar_una_asignacion_se_libera_cupo()
    {
        using var db = FabricaDeContextos.EnMemoria();
        FabricaDeContextos.SembrarBase(db);
        var asignaciones = new AsignacionService(db);

        var s1 = FabricaDeContextos.NuevoServicio(db, 1, "Mantenimiento", 100m);
        var s2 = FabricaDeContextos.NuevoServicio(db, 2, "Mantenimiento", 200m);
        var s3 = FabricaDeContextos.NuevoServicio(db, 3, "Mantenimiento", 300m);

        await asignaciones.Asignar(s1.IdServicio, 1, null);
        await asignaciones.Asignar(s2.IdServicio, 1, null);

        var primera = await db.DetalleServicio.FirstAsync(d => d.IdServicio == s1.IdServicio);
        var retiro = await asignaciones.Desasignar(primera.IdServicio, primera.IdTrabajador);
        Assert.True(retiro.EsCorrecto);

        var nuevo = await asignaciones.Asignar(s3.IdServicio, 1, null);
        Assert.True(nuevo.EsCorrecto);
    }
    // Regla 8: la fecha de fin no puede ser anterior a la de inicio

    [Fact]
    public async Task Regla8_Servicio_con_fecha_fin_anterior_es_rechazado_al_crear()
    {
        using var db = FabricaDeContextos.EnMemoria();
        FabricaDeContextos.SembrarBase(db);
        var servicio = new ServicioService(db);

        var resultado = await servicio.Crear(new Servicio
        {
            IdVehiculo = 1, TipoServicio = "Mantenimiento",
            Descripcion = "Prueba", FechaInicio = new DateOnly(2026, 9, 10),
            FechaFin = new DateOnly(2026, 9, 1), Total = 100m
        });

        Assert.False(resultado.EsCorrecto);
        Assert.Contains("fecha de finalización no puede ser anterior", resultado.Mensaje);
    }

    [Fact]
    public async Task Regla8_Finalizar_con_fecha_anterior_es_rechazado()
    {
        using var db = FabricaDeContextos.EnMemoria();
        FabricaDeContextos.SembrarBase(db);
        var servicioNegocio = new ServicioService(db);
        var creado = await servicioNegocio.Crear(new Servicio
        {
            IdVehiculo = 1, TipoServicio = "Mantenimiento",
            Descripcion = "Cambio de aceite", FechaInicio = new DateOnly(2026, 9, 10), Total = 1000m
        });

        var resultado = await servicioNegocio.Finalizar(creado.Datos!.IdServicio, new DateOnly(2026, 9, 1));

        Assert.False(resultado.EsCorrecto);
        Assert.Contains("fecha de finalización no puede ser anterior", resultado.Mensaje);
    }

    [Fact]
    public async Task Regla8_Finalizar_retira_a_los_trabajadores_asignados()
    {
        using var db = FabricaDeContextos.EnMemoria();
        FabricaDeContextos.SembrarBase(db);
        var servicioNegocio = new ServicioService(db);
        var creado = await servicioNegocio.Crear(new Servicio
        {
            IdVehiculo = 1, TipoServicio = "Mantenimiento",
            Descripcion = "Cambio de aceite", FechaInicio = new DateOnly(2026, 9, 10), Total = 1000m
        });
        var idServicio = creado.Datos!.IdServicio;

        await new AsignacionService(db).Asignar(idServicio, 1, null);
        var resultado = await servicioNegocio.Finalizar(idServicio, new DateOnly(2026, 9, 12));

        Assert.True(resultado.EsCorrecto);
        var servicio = await db.Servicios.SingleAsync(s => s.IdServicio == idServicio);
        Assert.Equal("Finalizado", servicio.Estado);
        Assert.Equal(new DateOnly(2026, 9, 12), servicio.FechaFin);
        var asignacion = await db.DetalleServicio.SingleAsync();
        Assert.Equal("Retirado", asignacion.Estado);
    }
    // Reglas 9 y 10: productos — pareja única, cantidades y precios válidos

    [Fact]
    public async Task Regla9_Un_producto_no_se_repite_en_el_mismo_servicio()
    {
        using var db = FabricaDeContextos.EnMemoria();
        FabricaDeContextos.SembrarBase(db);
        var servicio = FabricaDeContextos.NuevoServicio(db, 1, "Mantenimiento", 100m);
        var productos = new ProductoService(db);

        var primero = await productos.AgregarAlServicio(servicio.IdServicio, 1, 2m);
        var repetido = await productos.AgregarAlServicio(servicio.IdServicio, 1, 3m);

        Assert.True(primero.EsCorrecto);
        Assert.False(repetido.EsCorrecto);
        Assert.Contains("ya está registrado en el servicio", repetido.Mensaje);
        Assert.Equal(1, await db.ProductosUtilizados.CountAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Regla10_La_cantidad_debe_ser_mayor_que_cero(decimal cantidad)
    {
        using var db = FabricaDeContextos.EnMemoria();
        FabricaDeContextos.SembrarBase(db);
        var servicio = FabricaDeContextos.NuevoServicio(db, 1, "Mantenimiento", 100m);
        var productos = new ProductoService(db);

        var resultado = await productos.AgregarAlServicio(servicio.IdServicio, 1, cantidad);

        Assert.False(resultado.EsCorrecto);
        Assert.Contains("mayor que cero", resultado.Mensaje);
    }

    [Fact]
    public async Task Regla10_Producto_con_precio_negativo_es_rechazado()
    {
        using var db = FabricaDeContextos.EnMemoria();
        FabricaDeContextos.SembrarBase(db);
        var productos = new ProductoService(db);

        var resultado = await productos.Crear(new Producto
        {
            Nombre = "Repuesto inválido", Descripcion = "Prueba", PrecioUnitario = -1m, Stock = 5m
        });

        Assert.False(resultado.EsCorrecto);
        Assert.Contains("precio unitario", resultado.Mensaje, StringComparison.OrdinalIgnoreCase);
    }
    // Reglas 11, 12, 13 y 14: facturación

    [Fact]
    public async Task Regla11_Factura_sin_detalles_es_rechazada()
    {
        using var db = FabricaDeContextos.EnMemoria();
        FabricaDeContextos.SembrarBase(db);
        var facturacion = new FacturacionService(db, FabricaDeContextos.Configuracion());

        var resultado = await facturacion.CrearFactura(1, "Efectivo", new List<int>(), null);

        Assert.False(resultado.EsCorrecto);
        Assert.Contains("al menos un servicio", resultado.Mensaje);
    }

    [Fact]
    public async Task Regla11_Un_servicio_no_se_factura_dos_veces_con_factura_emitida()
    {
        using var db = FabricaDeContextos.EnMemoria();
        FabricaDeContextos.SembrarBase(db);
        var servicio = FabricaDeContextos.NuevoServicio(db, 1, "Mantenimiento", 1000m);
        var facturacion = new FacturacionService(db, FabricaDeContextos.Configuracion());

        var primera = await facturacion.CrearFactura(1, "Efectivo",
            new List<int> { servicio.IdServicio }, null);
        Assert.True(primera.EsCorrecto);

        var segunda = await facturacion.CrearFactura(1, "Efectivo",
            new List<int> { servicio.IdServicio }, null);

        Assert.False(segunda.EsCorrecto);
        Assert.Contains("factura emitida", segunda.Mensaje);
    }

    [Fact]
    public async Task Regla12_No_se_pueden_mezclar_servicios_de_clientes_distintos()
    {
        using var db = FabricaDeContextos.EnMemoria();
        FabricaDeContextos.SembrarBase(db);
        var delCliente1 = FabricaDeContextos.NuevoServicio(db, 1, "Mantenimiento", 1000m);
        var delCliente2 = FabricaDeContextos.NuevoServicio(db, 3, "Mantenimiento", 2000m);
        var facturacion = new FacturacionService(db, FabricaDeContextos.Configuracion());

        var resultado = await facturacion.CrearFactura(1, "Efectivo",
            new List<int> { delCliente1.IdServicio, delCliente2.IdServicio }, null);

        Assert.False(resultado.EsCorrecto);
        Assert.Contains("pertenece a otro cliente", resultado.Mensaje);
        Assert.Empty(await db.Facturas.ToListAsync());
    }

    [Fact]
    public async Task Regla13_Total_es_subtotal_mas_iva_calculado_en_el_servidor()
    {
        using var db = FabricaDeContextos.EnMemoria();
        FabricaDeContextos.SembrarBase(db);
        var s1 = FabricaDeContextos.NuevoServicio(db, 1, "Mantenimiento", 1000m);
        var s2 = FabricaDeContextos.NuevoServicio(db, 2, "Pintura", 500m);
        var facturacion = new FacturacionService(db, FabricaDeContextos.Configuracion(15m));

        var resultado = await facturacion.CrearFactura(1, "Tarjeta",
            new List<int> { s1.IdServicio, s2.IdServicio }, "Prueba de cálculo");

        Assert.True(resultado.EsCorrecto);
        var factura = resultado.Datos!;
        Assert.Equal(1500m, factura.Subtotal);
        Assert.Equal(225m, factura.IVA);            // 15 % de 1500
        Assert.Equal(1725m, factura.Total);         // 1500 + 225
        Assert.Equal(factura.Subtotal + factura.IVA, factura.Total);
        Assert.Equal(2, await db.DetalleFactura.CountAsync());
        Assert.StartsWith("FAC-", factura.NumeroFactura);
    }

    [Fact]
    public async Task Regla14_Anular_un_factura_conserva_el_registro_y_libera_los_servicios()
    {
        using var db = FabricaDeContextos.EnMemoria();
        FabricaDeContextos.SembrarBase(db);
        var servicio = FabricaDeContextos.NuevoServicio(db, 1, "Mantenimiento", 1000m);
        var facturacion = new FacturacionService(db, FabricaDeContextos.Configuracion());

        var creada = await facturacion.CrearFactura(1, "Efectivo",
            new List<int> { servicio.IdServicio }, null);
        Assert.True(creada.EsCorrecto);
        var idFactura = creada.Datos!.IdFactura;

        var anulada = await facturacion.AnularFactura(idFactura, "Error en el monto");
        Assert.True(anulada.EsCorrecto);

        var factura = await db.Facturas.SingleAsync(f => f.IdFactura == idFactura);
        Assert.Equal("Anulada", factura.Estado);
        Assert.Contains("Error en el monto", factura.Notas);
        Assert.Equal(1, await db.Facturas.CountAsync()); // no se borró

        // El servicio queda libre para volver a facturarse.
        var refacturada = await facturacion.CrearFactura(1, "Efectivo",
            new List<int> { servicio.IdServicio }, null);
        Assert.True(refacturada.EsCorrecto);
    }

    [Fact]
    public async Task Regla14_No_se_puede_anular_una_factura_ya_anulada()
    {
        using var db = FabricaDeContextos.EnMemoria();
        FabricaDeContextos.SembrarBase(db);
        var servicio = FabricaDeContextos.NuevoServicio(db, 1, "Mantenimiento", 700m);
        var facturacion = new FacturacionService(db, FabricaDeContextos.Configuracion());
        var creada = await facturacion.CrearFactura(1, "Efectivo", new List<int> { servicio.IdServicio }, null);
        await facturacion.AnularFactura(creada.Datos!.IdFactura, null);

        var segunda = await facturacion.AnularFactura(creada.Datos.IdFactura, null);

        Assert.False(segunda.EsCorrecto);
    }
    // Regla 14 (desactivación lógica): no hay borrados destructivos

    [Fact]
    public async Task Regla14_Desactivar_un_cliente_conserva_su_registro_y_vehiculos()
    {
        using var db = FabricaDeContextos.EnMemoria();
        FabricaDeContextos.SembrarBase(db);
        var servicio = new ClienteService(db);

        var resultado = await servicio.CambiarEstado(1, activo: false);

        Assert.True(resultado.EsCorrecto);
        var cliente = await db.Clientes.SingleAsync(c => c.IdCliente == 1);
        Assert.False(cliente.Activo);
        Assert.Equal(2, await db.Vehiculos.CountAsync(v => v.IdCliente == 1));
    }

    [Fact]
    public async Task Regla14_No_se_puede_desactivar_un_vehiculo_con_servicios_abiertos()
    {
        using var db = FabricaDeContextos.EnMemoria();
        FabricaDeContextos.SembrarBase(db);
        FabricaDeContextos.NuevoServicio(db, 1, "Mantenimiento", 100m);
        var servicio = new VehiculoService(db);

        var resultado = await servicio.CambiarEstado(1, activo: false);

        Assert.False(resultado.EsCorrecto);
        Assert.Contains("servicios abiertos", resultado.Mensaje);
    }
}
