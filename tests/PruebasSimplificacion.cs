using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TallerMecanicoSantoDomingo.Data;
using TallerMecanicoSantoDomingo.Models;
using TallerMecanicoSantoDomingo.Services;
using Xunit;

namespace TallerMecanicoSantoDomingo.Tests;

public class PruebasSimplificacion
{
    [Fact]
    public async Task Cedula_y_chasis_se_guardan_y_son_unicos_cuando_tienen_valor()
    {
        using var db = FabricaDeContextos.EnMemoria();
        FabricaDeContextos.SembrarBase(db);
        var clientes = new ClienteService(db);
        var vehiculos = new VehiculoService(db);
        var cliente = (await clientes.ObtenerPorId(1))!;
        var vehiculo = (await vehiculos.ObtenerPorId(1))!;
        cliente.Cedula = " 001-120690-0001a ";
        vehiculo.NumChasis = " abcd123 ";
        vehiculo.Kilometraje = 75000;
        vehiculo.Color = " Azul ";
        vehiculo.Tipo = " Sedán ";

        Assert.True((await clientes.Editar(cliente)).EsCorrecto);
        Assert.True((await vehiculos.Editar(vehiculo)).EsCorrecto);
        Assert.Equal("001-120690-0001A", (await clientes.ObtenerPorId(1))!.Cedula);
        var guardado = (await vehiculos.ObtenerPorId(1))!;
        Assert.Equal("ABCD123", guardado.NumChasis);
        Assert.Equal(75000, guardado.Kilometraje);
        Assert.Equal("Azul", guardado.Color);
        Assert.Equal("Sedán", guardado.Tipo);

        var otroCliente = (await clientes.ObtenerPorId(2))!;
        var otroVehiculo = (await vehiculos.ObtenerPorId(2))!;
        otroCliente.Cedula = "001-120690-0001a";
        otroVehiculo.NumChasis = "abcd123";
        Assert.False((await clientes.Editar(otroCliente)).EsCorrecto);
        Assert.False((await vehiculos.Editar(otroVehiculo)).EsCorrecto);

        cliente.Cedula = " ";
        vehiculo.NumChasis = " ";
        Assert.True((await clientes.Editar(cliente)).EsCorrecto);
        Assert.True((await vehiculos.Editar(vehiculo)).EsCorrecto);
        Assert.Null((await clientes.ObtenerPorId(1))!.Cedula);
        Assert.Null((await vehiculos.ObtenerPorId(1))!.NumChasis);
    }

    [Fact]
    public async Task Costo_de_compra_se_registra_y_se_edita_independiente_del_precio()
    {
        using var db = FabricaDeContextos.EnMemoria();
        var productos = new ProductoService(db);
        var producto = new Producto { Nombre = "Aceite", Descripcion = "Motor", Costo = 150m, PrecioUnitario = 210m };

        Assert.True((await productos.Crear(producto)).EsCorrecto);
        producto = (await productos.ObtenerPorId(producto.IdProducto))!;
        Assert.Equal(150m, producto.Costo);
        producto.Costo = 175m;
        Assert.True((await productos.Editar(producto, producto.Stock)).EsCorrecto);
        producto = (await productos.ObtenerPorId(producto.IdProducto))!;
        Assert.Equal(175m, producto.Costo);
        Assert.Equal(210m, producto.PrecioUnitario);

        producto.Costo = -1;
        Assert.False((await productos.Editar(producto, producto.Stock)).EsCorrecto);
    }

    [Fact]
    public async Task Modificar_y_quitar_productos_usa_la_pareja_correcta()
    {
        using var db = FabricaDeContextos.EnMemoria();
        FabricaDeContextos.SembrarBase(db);
        var primero = FabricaDeContextos.NuevoServicio(db, 1, "Mantenimiento", 100);
        var segundo = FabricaDeContextos.NuevoServicio(db, 2, "Mantenimiento", 200);
        var productos = new ProductoService(db);
        await productos.AgregarAlServicio(primero.IdServicio, 1, 2);
        await productos.AgregarAlServicio(segundo.IdServicio, 1, 3);

        Assert.True((await productos.ActualizarCantidad(primero.IdServicio, 1, 5)).EsCorrecto);
        Assert.Equal(5m, (await db.ProductosUtilizados.FindAsync(1, primero.IdServicio))!.Cantidad);
        Assert.Equal(3m, (await db.ProductosUtilizados.FindAsync(1, segundo.IdServicio))!.Cantidad);

        Assert.True((await productos.QuitarDelServicio(primero.IdServicio, 1)).EsCorrecto);
        Assert.Null(await db.ProductosUtilizados.FindAsync(1, primero.IdServicio));
        Assert.NotNull(await db.ProductosUtilizados.FindAsync(1, segundo.IdServicio));
    }

    [Fact]
    public async Task Compatibles_y_cargas_conservan_las_reglas_y_la_especialidad()
    {
        using var db = FabricaDeContextos.EnMemoria();
        FabricaDeContextos.SembrarBase(db);
        var primero = FabricaDeContextos.NuevoServicio(db, 1, "Mantenimiento", 100);
        var segundo = FabricaDeContextos.NuevoServicio(db, 2, "Mantenimiento", 200);
        var asignaciones = new AsignacionService(db);
        await asignaciones.Asignar(primero.IdServicio, 1, null);

        Assert.Empty(await asignaciones.TrabajadoresCompatibles(primero.IdServicio, false));
        var compatible = Assert.Single(await asignaciones.TrabajadoresCompatibles(segundo.IdServicio, true));
        Assert.Equal("Mantenimiento mecánico", compatible.Area);
        Assert.Equal(1, compatible.ServiciosActivos);
        Assert.True(compatible.Disponible);
        Assert.False((await asignaciones.Desasignar(primero.IdServicio, 1)).EsCorrecto);
        Assert.True((await new ServicioService(db).Finalizar(primero.IdServicio, new DateOnly(2026, 9, 2))).EsCorrecto);
        var carga = Assert.Single(await new ServicioService(db).CargasDeTrabajadores([1]));
        Assert.Equal(0, carga.ServiciosActivos);
    }

    [Fact]
    public async Task Reporte_de_productos_filtra_fechas_y_conserva_cantidades_e_importes()
    {
        using var db = FabricaDeContextos.EnMemoria();
        FabricaDeContextos.SembrarBase(db);
        var enero = FabricaDeContextos.NuevoServicio(db, 1, "Mantenimiento", 100);
        var febrero = FabricaDeContextos.NuevoServicio(db, 2, "Mantenimiento", 200);
        enero.FechaInicio = new DateOnly(2026, 1, 10);
        febrero.FechaInicio = new DateOnly(2026, 2, 10);
        await db.SaveChangesAsync();
        var productos = new ProductoService(db);
        await productos.AgregarAlServicio(enero.IdServicio, 1, 2);
        await productos.AgregarAlServicio(febrero.IdServicio, 1, 3);
        var reportes = new ReporteService(db);

        var mes = Assert.Single(await reportes.ProductosMasUtilizados(new(2026, 1, 1), new(2026, 1, 31)));
        Assert.Equal(2m, mes.CantidadTotal);
        Assert.Equal(1, mes.VecesUtilizado);
        Assert.Equal(700m, mes.MontoEstimado);
        var historico = Assert.Single(await reportes.ProductosMasUtilizados(null, null));
        Assert.Equal(5m, historico.CantidadTotal);
        Assert.Equal(1750m, historico.MontoEstimado);
    }

    [Fact]
    public async Task Factura_conserva_datos_fiscales_y_detalles_al_anular()
    {
        using var db = FabricaDeContextos.EnMemoria();
        FabricaDeContextos.SembrarBase(db);
        var servicio = FabricaDeContextos.NuevoServicio(db, 1, "Mantenimiento", 1000, estado: "Finalizado");
        var facturacion = new FacturacionService(db, FabricaDeContextos.Configuracion());
        var resultado = await facturacion.CrearFactura(1, "Efectivo", [servicio.IdServicio], null);
        Assert.True(resultado.EsCorrecto);
        Assert.Equal("RFC-0001", resultado.Datos!.RUC);
        Assert.Equal(1150m, resultado.Datos.Total);
        Assert.NotNull(await db.DetalleFactura.FindAsync(servicio.IdServicio, resultado.Datos.IdFactura));
        await facturacion.AnularFactura(resultado.Datos.IdFactura, "Corrección");
        Assert.Single(await db.DetalleFactura.ToListAsync());
        Assert.Single(await facturacion.ServiciosElegibles(1));
    }

    private static ServiceProvider ServiciosDeAcceso()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        services.AddDbContext<TallerDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddIdentityCore<IdentityUser>(o =>
        {
            o.User.RequireUniqueEmail = true;
            o.Lockout.MaxFailedAccessAttempts = 5;
            o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        }).AddUserStore<UsuarioStore>().AddSignInManager();
        services.AddAuthentication(o =>
        {
            o.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
            o.DefaultSignInScheme = IdentityConstants.ExternalScheme;
        }).AddIdentityCookies();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Usuario_independiente_del_trabajador_inicia_sesion_con_cookie()
    {
        using var services = ServiciosDeAcceso();
        using var scope = services.CreateScope();
        var gestor = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var user = new IdentityUser { UserName = "admin@taller.local", Email = "admin@taller.local" };
        Assert.True((await gestor.CreateAsync(user, "Seguro123!")).Succeeded);
        var db = scope.ServiceProvider.GetRequiredService<TallerDbContext>();
        Assert.Empty(await db.Trabajadores.ToListAsync());
        Assert.NotEqual("Seguro123!", user.PasswordHash);
        Assert.Equal(user.Id, (await gestor.FindByEmailAsync("ADMIN@TALLER.LOCAL"))!.Id);

        var ingreso = scope.ServiceProvider.GetRequiredService<SignInManager<IdentityUser>>();
        var http = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        ingreso.Context = http;
        var resultado = await ingreso.PasswordSignInAsync(user, "Seguro123!", true, true);
        Assert.True(resultado.Succeeded);
        Assert.Contains(".AspNetCore.Identity.Application", http.Response.Headers.SetCookie.ToString());
    }

    [Fact]
    public async Task Identity_bloquea_la_cuenta_despues_de_cinco_fallos()
    {
        using var services = ServiciosDeAcceso();
        using var scope = services.CreateScope();
        var gestor = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var user = new IdentityUser { UserName = "admin@taller.local", Email = "admin@taller.local" };
        Assert.True((await gestor.CreateAsync(user, "Seguro123!")).Succeeded);
        var ingreso = scope.ServiceProvider.GetRequiredService<SignInManager<IdentityUser>>();
        ingreso.Context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        for (var i = 0; i < 5; i++)
            Assert.False((await ingreso.CheckPasswordSignInAsync(user, "Incorrecta123!", true)).Succeeded);
        Assert.True(await gestor.IsLockedOutAsync(user));
        Assert.True((await ingreso.CheckPasswordSignInAsync(user, "Seguro123!", true)).IsLockedOut);
    }

    [Fact]
    public async Task Identity_rechaza_correo_duplicado_y_actualiza_hash_y_sello_de_seguridad()
    {
        using var services = ServiciosDeAcceso();
        using var scope = services.CreateScope();
        var gestor = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var user = new IdentityUser { UserName = "admin@taller.local", Email = "admin@taller.local" };
        Assert.True((await gestor.CreateAsync(user, "Seguro123!")).Succeeded);
        var duplicado = new IdentityUser { UserName = "otro@taller.local", Email = "ADMIN@TALLER.LOCAL" };
        Assert.False((await gestor.CreateAsync(duplicado, "Seguro123!")).Succeeded);
        var selloAnterior = user.SecurityStamp;
        Assert.True((await gestor.ChangePasswordAsync(user, "Seguro123!", "Nueva123!")).Succeeded);
        Assert.NotEqual(selloAnterior, user.SecurityStamp);
        Assert.False(await gestor.CheckPasswordAsync(user, "Seguro123!"));
        Assert.True(await gestor.CheckPasswordAsync(user, "Nueva123!"));
    }
}
