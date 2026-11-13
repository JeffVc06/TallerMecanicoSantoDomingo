using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using TallerMecanicoSantoDomingo.Models;
using TallerMecanicoSantoDomingo.Services;
using TallerMecanicoSantoDomingo.ViewModels;
using Xunit;

namespace TallerMecanicoSantoDomingo.Tests;

public class PruebasDuplicadosYContacto
{
    [Theory]
    [InlineData(false, true)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(true, false)]
    public async Task Documentos_no_se_reutilizan_al_crear_o_editar_incluso_si_el_titular_esta_inactivo(bool ruc, bool activo)
    {
        using var db = FabricaDeContextos.EnMemoria();
        var clientes = new ClienteService(db);
        var titular = ClienteDePrueba("001-120690-0001A", "J0310000000012");
        var otro = ClienteDePrueba("001-120690-0002B", "J0310000000013");
        Assert.True((await clientes.Crear(titular)).EsCorrecto);
        Assert.True((await clientes.Crear(otro)).EsCorrecto);
        Assert.True((await clientes.CambiarEstado(titular.IdCliente, activo)).EsCorrecto);

        var duplicado = ClienteDePrueba("001-120690-0003C", "J0310000000014");
        if (ruc) duplicado.RFC = " j0310000000012 ";
        else duplicado.Cedula = " 001-120690-0001a ";
        var creacion = await clientes.Crear(duplicado);
        Assert.False(creacion.EsCorrecto);
        Assert.Contains(ruc ? "RUC" : "cédula", creacion.Mensaje);

        var cambios = (await clientes.ObtenerPorId(otro.IdCliente))!;
        if (ruc) cambios.RFC = duplicado.RFC;
        else cambios.Cedula = duplicado.Cedula;
        var edicion = await clientes.Editar(cambios);
        Assert.False(edicion.EsCorrecto);
        Assert.Contains(ruc ? "RUC" : "cédula", edicion.Mensaje);
        var conservado = (await clientes.ObtenerPorId(otro.IdCliente))!;
        Assert.Equal("001-120690-0002B", conservado.Cedula);
        Assert.Equal("J0310000000013", conservado.RFC);
        Assert.Equal(2, await db.Clientes.CountAsync());

        var propio = (await clientes.ObtenerPorId(titular.IdCliente))!;
        propio.Cedula = " 001-120690-0001a ";
        propio.RFC = " j0310000000012 ";
        propio.Direccion = "León";
        Assert.True((await clientes.Editar(propio)).EsCorrecto);
        var actualizado = (await clientes.ObtenerPorId(titular.IdCliente))!;
        Assert.Equal("001-120690-0001A", actualizado.Cedula);
        Assert.Equal("J0310000000012", actualizado.RFC);
        Assert.Equal("León", actualizado.Direccion);
        Assert.Equal(activo, actualizado.Activo);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Telefono_compartido_se_admite_al_crear_y_editar_clientes_y_trabajadores(bool nuevo)
    {
        using var db = FabricaDeContextos.EnMemoria();
        var clientes = new ClienteService(db);
        var trabajadores = new TrabajadorService(db);
        Assert.True((await clientes.Crear(ClienteDePrueba("001-120690-0001A"))).EsCorrecto);
        Assert.True((await trabajadores.Guardar(TrabajadorDePrueba(), true)).EsCorrecto);

        var cliente = ClienteDePrueba("001-120690-0002B");
        var trabajador = TrabajadorDePrueba();
        if (!nuevo)
        {
            cliente.Telefono = trabajador.Telefono = "8777-2222";
            Assert.True((await clientes.Crear(cliente)).EsCorrecto);
            Assert.True((await trabajadores.Guardar(trabajador, true)).EsCorrecto);
            cliente.Telefono = trabajador.Telefono = "8888-1111";
        }

        var resultadoCliente = nuevo ? await clientes.Crear(cliente) : await clientes.Editar(cliente);
        Assert.True(resultadoCliente.EsCorrecto, resultadoCliente.Mensaje);
        var resultadoTrabajador = await trabajadores.Guardar(trabajador, nuevo);
        Assert.True(resultadoTrabajador.EsCorrecto, resultadoTrabajador.Mensaje);
        Assert.Equal(2, await db.Clientes.CountAsync(c => c.Telefono == "8888-1111"));
        Assert.Equal(2, await db.Trabajadores.CountAsync(t => t.Telefono == "8888-1111"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("sin telefono")]
    [InlineData("123456789012345678901")]
    public async Task Telefono_vacio_invalido_o_demasiado_largo_se_rechaza_en_formularios_y_al_guardar(string telefono)
    {
        ValidarFormularios(telefono, false);
        using var db = FabricaDeContextos.EnMemoria();
        var clientes = new ClienteService(db);
        var trabajadores = new TrabajadorService(db);
        var cliente = ClienteDePrueba("001-120690-0001A");
        var trabajador = TrabajadorDePrueba();
        Assert.True((await clientes.Crear(cliente)).EsCorrecto);
        Assert.True((await trabajadores.Guardar(trabajador, true)).EsCorrecto);

        var altaCliente = ClienteDePrueba("001-120690-0002B");
        altaCliente.Telefono = telefono;
        var altaTrabajador = TrabajadorDePrueba();
        altaTrabajador.Telefono = telefono;
        Assert.False((await clientes.Crear(altaCliente)).EsCorrecto);
        Assert.False((await trabajadores.Guardar(altaTrabajador, true)).EsCorrecto);

        cliente.Telefono = trabajador.Telefono = telefono;
        Assert.False((await clientes.Editar(cliente)).EsCorrecto);
        Assert.False((await trabajadores.Guardar(trabajador, false)).EsCorrecto);
        Assert.Equal("8888-1111", (await db.Clientes.AsNoTracking().SingleAsync()).Telefono);
        Assert.Equal("8888-1111", (await db.Trabajadores.AsNoTracking().SingleAsync()).Telefono);
    }

    [Theory]
    [InlineData("8888")]
    [InlineData("8888-1111")]
    [InlineData("+505 8888-1111")]
    public async Task Telefono_usa_formato_general_sin_exigir_ocho_digitos(string telefono)
    {
        ValidarFormularios(telefono, true);
        using var db = FabricaDeContextos.EnMemoria();
        var cliente = ClienteDePrueba("001-120690-0001A");
        cliente.Telefono = telefono;
        var trabajador = TrabajadorDePrueba();
        trabajador.Telefono = telefono;
        Assert.True((await new ClienteService(db).Crear(cliente)).EsCorrecto);
        Assert.True((await new TrabajadorService(db).Guardar(trabajador, true)).EsCorrecto);
    }

    [Fact]
    public async Task Identificaciones_no_imponen_mascara_ni_unifican_variantes_con_y_sin_guiones()
    {
        using var db = FabricaDeContextos.EnMemoria();
        var clientes = new ClienteService(db);
        var conGuiones = ClienteDePrueba("001-120690-0001A", "J031-0000000012");
        var sinGuiones = ClienteDePrueba("0011206900001A", "J0310000000012");
        var sinMascara = ClienteDePrueba("sin formato", "sin formato");

        // Documenta el límite actual: estos controles no acreditan un documento real.
        Assert.True((await clientes.Crear(conGuiones)).EsCorrecto);
        Assert.True((await clientes.Crear(sinGuiones)).EsCorrecto);
        Assert.True((await clientes.Crear(sinMascara)).EsCorrecto);
        Assert.Equal(3, await db.Clientes.CountAsync());
    }

    private static void ValidarFormularios(string telefono, bool valido)
    {
        object[] formularios =
        [
            new ClienteFormViewModel
            {
                Nombre = "Ana", Apellido = "López", Telefono = telefono,
                Direccion = "Managua", Cedula = "001-120690-0001A"
            },
            new TrabajadorFormViewModel
            {
                Nombre = "Ana", Apellido = "López", Telefono = telefono,
                Area = "Mantenimiento mecánico", FechaIngreso = new DateOnly(2026, 9, 1)
            }
        ];
        foreach (var formulario in formularios)
        {
            var errores = new List<ValidationResult>();
            Assert.Equal(valido, Validator.TryValidateObject(formulario, new ValidationContext(formulario), errores, true));
            if (!valido) Assert.Contains(errores, e => e.MemberNames.Contains("Telefono"));
        }
    }

    private static Cliente ClienteDePrueba(string cedula, string? ruc = null) => new()
    {
        Nombre = "Ana", Apellido = "López", Telefono = "8888-1111",
        Direccion = "Managua", Cedula = cedula, RFC = ruc
    };

    private static Trabajador TrabajadorDePrueba() => new()
    {
        Nombre = "Ana", Apellido = "López", Telefono = "8888-1111",
        Area = "Mantenimiento mecánico", FechaRegistro = new DateTime(2026, 9, 1)
    };
}
