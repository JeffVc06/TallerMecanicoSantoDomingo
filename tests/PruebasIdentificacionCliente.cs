using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using TallerMecanicoSantoDomingo.Models;
using TallerMecanicoSantoDomingo.Services;
using TallerMecanicoSantoDomingo.ViewModels;
using Xunit;

namespace TallerMecanicoSantoDomingo.Tests;

public class PruebasIdentificacionCliente
{
    [Theory]
    [InlineData(null, "001-120690-0001A", true)]
    [InlineData(" ", "001-120690-0001A", true)]
    [InlineData("J0310000000012", null, true)]
    [InlineData("J0310000000012", "001-120690-0001A", true)]
    [InlineData(null, null, false)]
    [InlineData(" ", " ", false)]
    public void Formulario_exige_cedula_o_ruc(string? ruc, string? cedula, bool valido)
    {
        var formulario = new ClienteFormViewModel
        {
            Nombre = "Ana", Apellido = "López", Telefono = "8888-1111",
            Direccion = "Managua", RFC = ruc, Cedula = cedula
        };
        var errores = new List<ValidationResult>();
        Assert.Equal(valido, Validator.TryValidateObject(formulario, new ValidationContext(formulario), errores, true));
        if (!valido)
        {
            var error = Assert.Single(errores);
            Assert.Contains(nameof(formulario.Cedula), error.MemberNames);
            Assert.Contains(nameof(formulario.RFC), error.MemberNames);
        }
    }

    [Fact]
    public async Task Varios_particulares_se_guardan_sin_ruc_y_se_buscan_por_cedula()
    {
        using var db = FabricaDeContextos.EnMemoria();
        var clientes = new ClienteService(db);
        var primero = Particular(" 001-120690-0001a ");
        var segundo = Particular("001-120690-0002B");
        segundo.RFC = " ";

        Assert.True((await clientes.Crear(primero)).EsCorrecto);
        Assert.True((await clientes.Crear(segundo)).EsCorrecto);
        Assert.Equal(2, await db.Clientes.CountAsync());
        Assert.All(await db.Clientes.ToListAsync(), c => Assert.Null(c.RFC));
        Assert.Equal("001-120690-0001A", primero.Cedula);
        Assert.Equal(primero.IdCliente, Assert.Single((await clientes.Buscar("0001A", true, 1, 10)).Elementos).IdCliente);
        Assert.Empty((await clientes.Buscar("sin coincidencias", true, 1, 10)).Elementos);

        var duplicado = await clientes.Crear(Particular("001-120690-0001a"));
        Assert.False(duplicado.EsCorrecto);
        Assert.Contains("cédula", duplicado.Mensaje);
    }

    [Fact]
    public async Task Crear_y_editar_rechazan_ambas_identificaciones_vacias()
    {
        using var db = FabricaDeContextos.EnMemoria();
        var clientes = new ClienteService(db);
        Assert.False((await clientes.Crear(Particular(" "))).EsCorrecto);
        Assert.Empty(await db.Clientes.ToListAsync());

        var cliente = Particular("001-120690-0001A");
        cliente.RFC = " J0310000000012 ";
        Assert.True((await clientes.Crear(cliente)).EsCorrecto);

        // Editar usa una instancia separada, igual que el controlador MVC.
        var cambios = Particular(cliente.Cedula!);
        cambios.IdCliente = cliente.IdCliente;
        cambios.RFC = " ";
        Assert.True((await clientes.Editar(cambios)).EsCorrecto);
        Assert.Null((await clientes.ObtenerPorId(cliente.IdCliente))!.RFC);

        cambios.Cedula = null;
        Assert.False((await clientes.Editar(cambios)).EsCorrecto);
        Assert.Equal("001-120690-0001A", (await clientes.ObtenerPorId(cliente.IdCliente))!.Cedula);
    }

    [Fact]
    public async Task Particular_sin_ruc_puede_facturar_y_buscar_su_factura_por_cedula()
    {
        using var db = FabricaDeContextos.EnMemoria();
        FabricaDeContextos.SembrarBase(db);
        var cliente = Particular("001-120690-0001A");
        cliente.IdCliente = 1;
        Assert.True((await new ClienteService(db).Editar(cliente)).EsCorrecto);
        var trabajo = FabricaDeContextos.NuevoServicio(db, 1, "Mantenimiento", 1000m, estado: "Finalizado");
        var facturas = new FacturacionService(db, FabricaDeContextos.Configuracion(15m));

        var resultado = await facturas.CrearFactura(1, "Efectivo", [trabajo.IdServicio], null);

        Assert.True(resultado.EsCorrecto, resultado.Mensaje);
        Assert.Null(resultado.Datos!.RUC);
        Assert.Equal(1000m, resultado.Datos.Subtotal);
        Assert.Equal(150m, resultado.Datos.IVA);
        Assert.Equal(1150m, resultado.Datos.Total);
        var encontrada = await facturas.Buscar(cliente.Cedula, null, null, null, 1, 10);
        Assert.Equal(resultado.Datos.IdFactura, Assert.Single(encontrada.Elementos).IdFactura);
        Assert.Empty((await facturas.Buscar("sin coincidencias", null, null, null, 1, 10)).Elementos);
    }

    private static Cliente Particular(string cedula) => new()
    {
        Nombre = "Ana", Apellido = "López", Telefono = "8888-1111",
        Direccion = "Managua", Cedula = cedula
    };
}
