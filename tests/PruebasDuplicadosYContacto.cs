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
        var otro = ClienteDePrueba("001-120690-0002B", "J0310000000013", "87772222");
        Assert.True((await clientes.Crear(titular)).EsCorrecto);
        Assert.True((await clientes.Crear(otro)).EsCorrecto);
        Assert.True((await clientes.CambiarEstado(titular.IdCliente, activo)).EsCorrecto);

        var duplicado = ClienteDePrueba("001-120690-0003C", "J0310000000014", "86663333");
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
    [InlineData(true, true, true)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, true)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    [InlineData(false, false, false)]
    public async Task Telefono_no_se_repite_al_crear_o_editar_en_ningun_modulo_incluso_con_titular_inactivo(
        bool titularCliente, bool destinoCliente, bool activo)
    {
        using var db = FabricaDeContextos.EnMemoria();
        var clientes = new ClienteService(db);
        var trabajadores = new TrabajadorService(db);
        if (titularCliente)
        {
            var titular = ClienteDePrueba("001-120690-0001A");
            Assert.True((await clientes.Crear(titular)).EsCorrecto);
            Assert.True((await clientes.CambiarEstado(titular.IdCliente, activo)).EsCorrecto);
        }
        else
        {
            var titular = TrabajadorDePrueba("88881111");
            Assert.True((await trabajadores.Guardar(titular, true)).EsCorrecto);
            Assert.True((await trabajadores.CambiarEstado(titular.IdTrabajador, activo)).EsCorrecto);
        }

        var cliente = ClienteDePrueba("001-120690-0002B");
        var trabajador = TrabajadorDePrueba("88881111");
        var alta = destinoCliente ? await clientes.Crear(cliente) : await trabajadores.Guardar(trabajador, true);
        Assert.False(alta.EsCorrecto);
        Assert.Contains("teléfono ya está registrado", alta.Mensaje);
        Assert.Equal(1, await db.Clientes.CountAsync() + await db.Trabajadores.CountAsync());

        cliente.Telefono = trabajador.Telefono = "87772222";
        Assert.True((destinoCliente ? await clientes.Crear(cliente) : await trabajadores.Guardar(trabajador, true)).EsCorrecto);
        cliente.Telefono = trabajador.Telefono = "88881111";
        cliente.Nombre = trabajador.Nombre = "Cambio rechazado";
        var edicion = destinoCliente ? await clientes.Editar(cliente) : await trabajadores.Guardar(trabajador, false);
        Assert.False(edicion.EsCorrecto);
        Assert.Contains("teléfono ya está registrado", edicion.Mensaje);
        if (destinoCliente)
        {
            var conservado = (await clientes.ObtenerPorId(cliente.IdCliente))!;
            Assert.Equal("87772222", conservado.Telefono);
            Assert.Equal("Ana", conservado.Nombre);
        }
        else
        {
            var conservado = await db.Trabajadores.AsNoTracking().SingleAsync(t => t.IdTrabajador == trabajador.IdTrabajador);
            Assert.Equal("87772222", conservado.Telefono);
            Assert.Equal("Ana", conservado.Nombre);
        }

        cliente.Telefono = trabajador.Telefono = "87772222";
        var propia = destinoCliente ? await clientes.Editar(cliente) : await trabajadores.Guardar(trabajador, false);
        Assert.True(propia.EsCorrecto, propia.Mensaje);
        Assert.Equal(2, await db.Clientes.CountAsync() + await db.Trabajadores.CountAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("8888")]
    [InlineData("8888111")]
    [InlineData("888811111")]
    [InlineData("8888-1111")]
    [InlineData("+505 8888-1111")]
    [InlineData("8888 1111")]
    [InlineData("8888.1111")]
    [InlineData("(8888)1111")]
    [InlineData("abcd1234")]
    [InlineData("٨٨٨٨١١١١")]
    [InlineData("８８８８１１１１")]
    [InlineData("88881111\n")]
    [InlineData(" 88881111")]
    [InlineData("88881111 ")]
    [InlineData("sin telefono")]
    [InlineData("123456789012345678901")]
    public async Task Telefono_sin_exactamente_ocho_digitos_se_rechaza_en_formularios_y_al_crear_o_editar(string? telefono)
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
        altaCliente.Telefono = telefono!;
        var altaTrabajador = TrabajadorDePrueba();
        altaTrabajador.Telefono = telefono!;
        Assert.False((await clientes.Crear(altaCliente)).EsCorrecto);
        Assert.False((await trabajadores.Guardar(altaTrabajador, true)).EsCorrecto);

        cliente.Telefono = trabajador.Telefono = telefono!;
        Assert.False((await clientes.Editar(cliente)).EsCorrecto);
        Assert.False((await trabajadores.Guardar(trabajador, false)).EsCorrecto);
        Assert.Equal("88881111", (await db.Clientes.AsNoTracking().SingleAsync()).Telefono);
        Assert.Equal("85554444", (await db.Trabajadores.AsNoTracking().SingleAsync()).Telefono);
    }

    [Theory]
    [InlineData("88881111")]
    [InlineData("00001234")]
    [InlineData("01234567")]
    [InlineData("12345678")]
    public async Task Telefono_de_ocho_digitos_se_admite_y_se_conserva_al_editar(string telefono)
    {
        ValidarFormularios(telefono, true);
        foreach (var esCliente in new[] { true, false })
        {
            using var db = FabricaDeContextos.EnMemoria();
            if (esCliente)
            {
                var clientes = new ClienteService(db);
                var cliente = ClienteDePrueba("001-120690-0001A", telefono: telefono);
                Assert.True((await clientes.Crear(cliente)).EsCorrecto);
                cliente.Nombre = "Nombre corregido";
                Assert.True((await clientes.Editar(cliente)).EsCorrecto);
                Assert.Equal(telefono, (await clientes.ObtenerPorId(cliente.IdCliente))!.Telefono);
            }
            else
            {
                var trabajadores = new TrabajadorService(db);
                var trabajador = TrabajadorDePrueba(telefono);
                Assert.True((await trabajadores.Guardar(trabajador, true)).EsCorrecto);
                trabajador.Nombre = "Nombre corregido";
                Assert.True((await trabajadores.Guardar(trabajador, false)).EsCorrecto);
                Assert.Equal(telefono, (await db.Trabajadores.AsNoTracking().SingleAsync()).Telefono);
            }
        }
    }

    [Theory]
    [InlineData("8888-1111", true)]
    [InlineData("8888-1111", false)]
    [InlineData("+505 8888-1111", true)]
    [InlineData("+505 8888-1111", false)]
    [InlineData("00505 8888 1111", true)]
    [InlineData("00505 8888 1111", false)]
    [InlineData(" (8888) 1111 ", true)]
    [InlineData(" (8888) 1111 ", false)]
    [InlineData("8888.1111", true)]
    [InlineData("8888.1111", false)]
    public async Task Telefono_previo_con_separadores_o_prefijo_no_se_puede_reutilizar(string telefonoPrevio, bool titularCliente)
    {
        using var db = FabricaDeContextos.EnMemoria();
        if (titularCliente)
        {
            var previo = ClienteDePrueba("001-120690-0001A", telefono: telefonoPrevio);
            previo.Activo = false;
            db.Clientes.Add(previo);
        }
        else
        {
            var previo = TrabajadorDePrueba(telefonoPrevio);
            previo.Activo = false;
            db.Trabajadores.Add(previo);
        }
        await db.SaveChangesAsync();
        var clientes = new ClienteService(db);
        var trabajadores = new TrabajadorService(db);
        var cliente = ClienteDePrueba("001-120690-0002B");
        var trabajador = TrabajadorDePrueba("88881111");
        Assert.False((await clientes.Crear(cliente)).EsCorrecto);
        Assert.False((await trabajadores.Guardar(trabajador, true)).EsCorrecto);

        cliente.Telefono = "87772222";
        trabajador.Telefono = "86663333";
        Assert.True((await clientes.Crear(cliente)).EsCorrecto);
        Assert.True((await trabajadores.Guardar(trabajador, true)).EsCorrecto);
        cliente.Telefono = trabajador.Telefono = "88881111";
        Assert.False((await clientes.Editar(cliente)).EsCorrecto);
        Assert.False((await trabajadores.Guardar(trabajador, false)).EsCorrecto);
        Assert.Equal("87772222", (await clientes.ObtenerPorId(cliente.IdCliente))!.Telefono);
        Assert.Equal("86663333", (await db.Trabajadores.AsNoTracking().SingleAsync(t => t.IdTrabajador == trabajador.IdTrabajador)).Telefono);
        var conservado = titularCliente
            ? await db.Clientes.Where(c => !c.Activo).Select(c => c.Telefono).SingleAsync()
            : await db.Trabajadores.Where(t => !t.Activo).Select(t => t.Telefono).SingleAsync();
        Assert.Equal(telefonoPrevio, conservado);
    }

    [Fact]
    public async Task Identificaciones_no_imponen_mascara_ni_unifican_variantes_con_y_sin_guiones()
    {
        using var db = FabricaDeContextos.EnMemoria();
        var clientes = new ClienteService(db);
        var conGuiones = ClienteDePrueba("001-120690-0001A", "J031-0000000012");
        var sinGuiones = ClienteDePrueba("0011206900001A", "J0310000000012", "87772222");
        var sinMascara = ClienteDePrueba("sin formato", "sin formato", "86663333");

        // Documenta el límite actual: estos controles no acreditan un documento real.
        Assert.True((await clientes.Crear(conGuiones)).EsCorrecto);
        Assert.True((await clientes.Crear(sinGuiones)).EsCorrecto);
        Assert.True((await clientes.Crear(sinMascara)).EsCorrecto);
        Assert.Equal(3, await db.Clientes.CountAsync());
    }

    private static void ValidarFormularios(string? telefono, bool valido)
    {
        object[] formularios =
        [
            new ClienteFormViewModel
            {
                Nombre = "Ana", Apellido = "López", Telefono = telefono!,
                Direccion = "Managua", Cedula = "001-120690-0001A"
            },
            new TrabajadorFormViewModel
            {
                Nombre = "Ana", Apellido = "López", Telefono = telefono!,
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

    private static Cliente ClienteDePrueba(string cedula, string? ruc = null, string telefono = "88881111") => new()
    {
        Nombre = "Ana", Apellido = "López", Telefono = telefono,
        Direccion = "Managua", Cedula = cedula, RFC = ruc
    };

    private static Trabajador TrabajadorDePrueba(string telefono = "85554444") => new()
    {
        Nombre = "Ana", Apellido = "López", Telefono = telefono,
        Area = "Mantenimiento mecánico", FechaRegistro = new DateTime(2026, 9, 1)
    };
}
