using System.Globalization;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using TallerMecanicoSantoDomingo.Controllers;
using TallerMecanicoSantoDomingo.ViewModels;
using Xunit;

namespace TallerMecanicoSantoDomingo.Tests;

public class PruebasEntradasMvc
{
    [Theory]
    [InlineData("Stock", "no es un número")]
    [InlineData("Stock", "999999999999999999999999999999999999")]
    [InlineData("PrecioUnitario", null)]
    public async Task Model_binding_rechaza_conversiones_y_valores_requeridos_ausentes(string campo, string? valor)
    {
        var datos = new Dictionary<string, StringValues>
        {
            ["Nombre"] = "Aceite", ["Descripcion"] = "Lubricante", ["PrecioUnitario"] = "100", ["Stock"] = "10", ["Costo"] = "50"
        };
        if (valor is null) datos.Remove(campo); else datos[campo] = valor;
        var estado = await Enlazar<ProductoFormViewModel>(datos);
        Assert.False(estado.IsValid);
        Assert.True(estado[campo]!.Errors.Count > 0);
    }

    [Fact]
    public async Task Fecha_final_omitida_no_se_sustituye_silenciosamente_por_hoy()
    {
        var estado = await Enlazar<FinalizarServicioViewModel>(new() { ["IdServicio"] = "1" });
        Assert.False(estado.IsValid);
        Assert.True(estado[nameof(FinalizarServicioViewModel.FechaFin)]!.Errors.Count > 0);
    }

    [Fact]
    public async Task Campos_con_valor_inicial_no_suplen_datos_requeridos_omitidos()
    {
        var vehiculo = await Enlazar<VehiculoFormViewModel>(new() { ["IdCliente"] = "1", ["Placa"] = "M 123456", ["Marca"] = "Toyota", ["Modelo"] = "Corolla" });
        Assert.NotEmpty(vehiculo[nameof(VehiculoFormViewModel.Anio)]!.Errors);
        var trabajador = await Enlazar<TrabajadorFormViewModel>(new() { ["Nombre"] = "José", ["Apellido"] = "De la Cruz", ["Telefono"] = "8888-1111", ["Area"] = "Pintura" });
        Assert.NotEmpty(trabajador[nameof(TrabajadorFormViewModel.FechaIngreso)]!.Errors);
        var servicio = await Enlazar<ServicioFormViewModel>(new() { ["IdVehiculo"] = "1", ["IdTrabajadorInicial"] = "1", ["Descripcion"] = "Prueba", ["DescripcionFalla"] = "Ruido", ["FechaInicio"] = "2026-09-01", ["Total"] = "100" });
        Assert.NotEmpty(servicio[nameof(ServicioFormViewModel.TipoServicio)]!.Errors);
        var factura = await Enlazar<FacturaCrearViewModel>(new() { ["IdCliente"] = "1", ["IdServicios[0]"] = "1" });
        Assert.NotEmpty(factura[nameof(FacturaCrearViewModel.MetodoPago)]!.Errors);
    }

    [Fact]
    public async Task Formulario_normal_de_producto_con_decimales_sigue_siendo_valido()
    {
        var estado = await Enlazar<ProductoFormViewModel>(new()
        {
            ["Nombre"] = "Aceite", ["Descripcion"] = "Lubricante", ["PrecioUnitario"] = "100.25", ["Stock"] = "10.125", ["Costo"] = "50.10"
        });
        Assert.True(estado.IsValid);
    }

    [Fact]
    public void Operaciones_del_taller_conservan_autenticacion_y_antiforgery()
    {
        var controladores = new[] { typeof(ClientesController), typeof(VehiculosController), typeof(ServiciosController),
            typeof(TrabajadoresController), typeof(ProductosController), typeof(AsignacionesController), typeof(FacturasController) };
        foreach (var controlador in controladores)
        {
            Assert.NotNull(controlador.GetCustomAttribute<AuthorizeAttribute>());
            foreach (var accion in controlador.GetMethods().Where(m => m.GetCustomAttribute<HttpPostAttribute>() != null))
            {
                Assert.Null(accion.GetCustomAttribute<AllowAnonymousAttribute>());
                Assert.NotNull(accion.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
            }
        }
    }

    private static async Task<ModelStateDictionary> Enlazar<T>(Dictionary<string, StringValues> datos)
    {
        var servicios = new ServiceCollection();
        servicios.AddLogging();
        servicios.AddMvcCore().AddDataAnnotations();
        using var proveedor = servicios.BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = proveedor };
        var accion = new ActionContext(http, new RouteData(), new ActionDescriptor(), new ModelStateDictionary());
        var metadatos = proveedor.GetRequiredService<IModelMetadataProvider>().GetMetadataForType(typeof(T));
        var binder = proveedor.GetRequiredService<IModelBinderFactory>().CreateBinder(new ModelBinderFactoryContext
        {
            Metadata = metadatos, BindingInfo = new BindingInfo(), CacheToken = typeof(T)
        });
        var contexto = DefaultModelBindingContext.CreateBindingContext(accion,
            new FormValueProvider(BindingSource.Form, new FormCollection(datos), CultureInfo.InvariantCulture), metadatos, new BindingInfo(), "");
        await binder.BindModelAsync(contexto);
        proveedor.GetRequiredService<IObjectModelValidator>().Validate(accion, null, "", contexto.Result.Model);
        return accion.ModelState;
    }
}
