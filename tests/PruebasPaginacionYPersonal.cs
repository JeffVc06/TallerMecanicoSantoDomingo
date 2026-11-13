using Microsoft.EntityFrameworkCore;
using TallerMecanicoSantoDomingo.Models;
using TallerMecanicoSantoDomingo.Services;
using Xunit;

namespace TallerMecanicoSantoDomingo.Tests;

public class PruebasPaginacionYPersonal
{
    [Theory]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    [InlineData(0)]
    public async Task Paginas_extremas_no_desbordan_ni_ocultan_registros(int pagina)
    {
        using var db = FabricaDeContextos.EnMemoria();
        FabricaDeContextos.SembrarBase(db);
        var servicio = FabricaDeContextos.NuevoServicio(db, 1, "Mantenimiento", 100, estado: "Finalizado");
        var facturacion = new FacturacionService(db, FabricaDeContextos.Configuracion());
        Assert.True((await facturacion.CrearFactura(1, "Efectivo", [servicio.IdServicio], null)).EsCorrecto);
        var clientes = await new ClienteService(db).Buscar(null, null, pagina, 10);
        var vehiculos = await new VehiculoService(db).Buscar(null, null, null, pagina, 10);
        var servicios = await new ServicioService(db).Buscar(null, null, null, null, pagina, 10);
        var productos = await new ProductoService(db).Buscar(null, null, pagina, 10);
        var facturas = await facturacion.Buscar(null, null, null, null, pagina, 10);
        Assert.Equal(1, clientes.Pagina); Assert.Equal(2, clientes.Elementos.Count);
        Assert.Equal(1, vehiculos.Pagina); Assert.Equal(3, vehiculos.Elementos.Count);
        Assert.Equal(1, servicios.Pagina); Assert.Single(servicios.Elementos);
        Assert.Equal(1, productos.Pagina); Assert.Equal(2, productos.Elementos.Count);
        Assert.Equal(1, facturas.Pagina); Assert.Single(facturas.Elementos);
    }

    [Fact]
    public async Task Pagina_fuera_de_rango_se_ajusta_a_la_ultima_y_admite_nombres_repetidos()
    {
        using var db = FabricaDeContextos.EnMemoria();
        for (var i = 0; i < 25; i++)
            db.Productos.Add(new Producto { Nombre = "Repuesto", Descripcion = "Producto de prueba" });
        await db.SaveChangesAsync();
        var productos = new ProductoService(db);
        var ultima = await productos.Buscar(null, null, int.MaxValue, 10);
        Assert.Equal(3, ultima.Pagina);
        Assert.Equal(25, ultima.TotalElementos);
        Assert.Equal(5, ultima.Elementos.Count);
        var segunda = await productos.Buscar(null, null, 2, 10);
        Assert.Empty(segunda.Elementos.Select(p => p.IdProducto).Intersect(ultima.Elementos.Select(p => p.IdProducto)));
        var vacia = await productos.Buscar("inexistente", null, int.MaxValue, 10);
        Assert.Equal(1, vacia.Pagina);
        Assert.Empty(vacia.Elementos);
    }

    [Theory]
    [InlineData("Finalizado")]
    [InlineData("Cancelado")]
    public async Task Servicios_cerrados_no_ofrecen_nuevas_asignaciones(string estado)
    {
        using var db = FabricaDeContextos.EnMemoria();
        FabricaDeContextos.SembrarBase(db);
        var servicio = FabricaDeContextos.NuevoServicio(db, 1, "Mantenimiento", 100, estado: estado);
        var asignaciones = new AsignacionService(db);
        Assert.Empty(await asignaciones.TrabajadoresCompatibles(servicio.IdServicio, false));
        Assert.False((await asignaciones.Asignar(servicio.IdServicio, 1, null)).EsCorrecto);
    }

    [Fact]
    public async Task Personal_habilitado_se_distingue_del_historial_para_conservar_un_reemplazo()
    {
        using var db = FabricaDeContextos.EnMemoria();
        FabricaDeContextos.SembrarBase(db);
        var servicio = FabricaDeContextos.NuevoServicio(db, 1, "Mantenimiento", 100);
        var asignaciones = new AsignacionService(db);
        Assert.True((await asignaciones.Asignar(servicio.IdServicio, 1, null)).EsCorrecto);
        Assert.True(Assert.Single(await asignaciones.AsignacionesDelServicio(servicio.IdServicio)).Habilitado);
        var trabajador = (await db.Trabajadores.FindAsync(1))!;
        trabajador.FechaSalida = DateOnly.FromDateTime(trabajador.FechaRegistro).AddDays(1);
        await db.SaveChangesAsync(); // Simula un dato previo; la lectura no lo repara.
        Assert.False(Assert.Single(await asignaciones.AsignacionesDelServicio(servicio.IdServicio)).Habilitado);
    }
}
