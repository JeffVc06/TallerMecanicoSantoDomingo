using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TallerMecanicoSantoDomingo.Controllers;
using TallerMecanicoSantoDomingo.Data;
using TallerMecanicoSantoDomingo.Services;
using Xunit;

namespace TallerMecanicoSantoDomingo.Tests;

/// <summary>HTTP, Razor, binding y persistencia en otro contexto EF InMemory. No utiliza SQL Server.</summary>
public class PruebasCambiosEstadoMvc : IAsyncLifetime
{
    private WebApplication _app = null!;
    private HttpClient _cliente = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(ClientesController).Assembly.FullName,
            EnvironmentName = "Testing", ContentRootPath = AppContext.BaseDirectory
        });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        // La misma base de prueba se comparte entre las solicitudes, con un contexto nuevo en cada una.
        var nombreBase = Guid.NewGuid().ToString();
        builder.Services.AddDbContext<TallerDbContext>(o => o.UseInMemoryDatabase(nombreBase)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddAuthentication("Pruebas").AddScheme<AuthenticationSchemeOptions, AccesoPruebas>("Pruebas", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(ClientesController).Assembly);
        builder.Services.AddScoped<ClienteService>();
        builder.Services.AddScoped<VehiculoService>();
        builder.Services.AddScoped<TrabajadorService>();
        builder.Services.AddScoped<ProductoService>();
        builder.Services.AddScoped<ServicioService>();
        builder.Services.AddScoped<AsignacionService>();
        builder.Services.AddScoped<FacturacionService>();
        builder.Services.AddScoped<ReporteService>();
        builder.Services.Configure<ConfiguracionTaller>(_ => { });
        _app = builder.Build();
        _app.UseRouting();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapControllerRoute("default", "{controller=Dashboard}/{action=Index}/{id?}");
        using (var scope = _app.Services.CreateScope())
            FabricaDeContextos.SembrarBase(scope.ServiceProvider.GetRequiredService<TallerDbContext>());
        await _app.StartAsync();
        var direccion = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        _cliente = new HttpClient(new HttpClientHandler { UseCookies = true, AllowAutoRedirect = true }) { BaseAddress = new Uri(direccion) };
    }

    public async Task DisposeAsync()
    {
        _cliente.Dispose();
        await _app.DisposeAsync();
    }

    [Theory]
    [InlineData("Clientes", true)]
    [InlineData("Clientes", false)]
    [InlineData("Trabajadores", true)]
    [InlineData("Trabajadores", false)]
    public async Task Formulario_de_contacto_exige_ocho_digitos_y_rechaza_telefono_del_otro_modulo(string modulo, bool nuevo)
    {
        var ruta = $"/{modulo}/{(nuevo ? "Crear" : "Editar/1")}";
        var html = await _cliente.GetStringAsync(ruta);
        var campo = Regex.Match(html, "<input\\b(?=[^>]*name=\"Telefono\")[^>]*>").Value;
        Assert.Contains("type=\"tel\"", campo);
        Assert.Contains("pattern=\"[0-9]{8}\"", campo);
        Assert.Contains("minlength=\"8\"", campo);
        Assert.Contains("maxlength=\"8\"", campo);
        var datos = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Valor(html, "__RequestVerificationToken"),
            ["Nombre"] = "Contacto", ["Apellido"] = "Prueba"
        };
        if (modulo == "Clientes")
        {
            datos["IdCliente"] = nuevo ? "0" : "1";
            datos["Cedula"] = "CONTACTO-PRUEBA";
            datos["Direccion"] = "Managua";
        }
        else
        {
            datos["IdTrabajador"] = nuevo ? "0" : "1";
            datos["Area"] = "Mantenimiento mecánico";
            datos["FechaIngreso"] = "2026-09-01";
        }

        datos["Telefono"] = "81234567";
        datos["Nombre"] = "Ana123";
        var nombreInvalido = await _cliente.PostAsync(ruta, new FormUrlEncodedContent(datos));
        Assert.Contains(ReglasTaller.ErrorNombre, WebUtility.HtmlDecode(await nombreInvalido.Content.ReadAsStringAsync()));
        datos["Nombre"] = "Contacto";
        if (modulo == "Trabajadores")
        {
            datos["Correo"] = "ana@dominio";
            var correoInvalido = await _cliente.PostAsync(ruta, new FormUrlEncodedContent(datos));
            Assert.Contains(ReglasTaller.ErrorCorreo, WebUtility.HtmlDecode(await correoInvalido.Content.ReadAsStringAsync()));
            datos.Remove("Correo");
        }
        var repetido = modulo == "Clientes" ? "85554444" : "88881111";
        foreach (var telefono in new[] { "8888", repetido, "81234567" })
        {
            datos["Telefono"] = telefono;
            var respuesta = await _cliente.PostAsync(ruta, new FormUrlEncodedContent(datos));
            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
            html = WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync());
            if (telefono == "8888") Assert.Contains(ReglasTaller.ErrorTelefono, html);
            if (telefono == repetido) Assert.Contains("El teléfono ya está registrado", html);

            using var scope = _app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TallerDbContext>();
            var telefonos = modulo == "Clientes"
                ? await db.Clientes.AsNoTracking().OrderBy(c => c.IdCliente).Select(c => c.Telefono).ToListAsync()
                : await db.Trabajadores.AsNoTracking().OrderBy(t => t.IdTrabajador).Select(t => t.Telefono).ToListAsync();
            if (telefono == "81234567")
            {
                Assert.Equal(nuevo ? 3 : 2, telefonos.Count);
                Assert.Equal(telefono, nuevo ? telefonos[^1] : telefonos[0]);
            }
            else
            {
                Assert.Equal(2, telefonos.Count);
                Assert.Equal(modulo == "Clientes" ? "88881111" : "85554444", telefonos[0]);
            }
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Formulario_de_vehiculo_rechaza_placa_equivalente_anio_fuera_de_rango_y_chasis_invalido(bool nuevo)
    {
        var ruta = nuevo ? "/Vehiculos/Crear" : "/Vehiculos/Editar/1";
        var html = await _cliente.GetStringAsync(ruta);
        var campoAnio = Regex.Match(html, "<input\\b(?=[^>]*name=\"Anio\")[^>]*>").Value;
        Assert.Contains($"max=\"{DateTime.Now.Year + 1}\"", campoAnio);
        var token = Valor(html, "__RequestVerificationToken");
        foreach (var caso in new[] { "placa", "anio", "chasis", "valido" })
        {
            var datos = new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token, ["IdVehiculo"] = nuevo ? "0" : "1", ["IdCliente"] = "1",
                ["Placa"] = caso == "placa" ? "mgb5678" : "M NUEVA9", ["Marca"] = "Toyota", ["Modelo"] = "Corolla",
                ["Anio"] = (caso == "anio" ? DateTime.Now.Year + 2 : 2020).ToString(),
                ["NumChasis"] = caso == "chasis" ? "***" : "ABCD999"
            };
            var respuesta = await _cliente.PostAsync(ruta, new FormUrlEncodedContent(datos));
            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
            html = WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync());
            if (caso == "placa") Assert.Contains("Ya existe un vehículo registrado con la placa", html);
            if (caso == "anio") Assert.Contains(ReglasTaller.ErrorAnio, html);
            if (caso == "chasis") Assert.Contains(ReglasTaller.ErrorIdentificadorVehiculo, html);
            using var scope = _app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TallerDbContext>();
            var vehiculos = await db.Vehiculos.AsNoTracking().OrderBy(v => v.IdVehiculo).ToListAsync();
            Assert.Equal(caso == "valido" && nuevo ? 4 : 3, vehiculos.Count);
            if (caso == "valido") Assert.Equal("M NUEVA9", nuevo ? vehiculos[^1].Placa : vehiculos[0].Placa);
            else
            {
                Assert.Equal("MGA-1234", vehiculos[0].Placa);
                Assert.Equal(2018, vehiculos[0].Anio);
                Assert.Null(vehiculos[0].NumChasis);
            }
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Formulario_de_producto_muestra_error_de_precision_y_conserva_saldo_tras_rechazo(bool nuevo)
    {
        var ruta = nuevo ? "/Productos/Crear" : "/Productos/Editar/1";
        var token = Valor(await _cliente.GetStringAsync(ruta), "__RequestVerificationToken");
        foreach (var caso in new[] { "precio", "stock", "valido" })
        {
            var datos = new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token, ["IdProducto"] = nuevo ? "0" : "1",
                ["StockOriginal"] = "20", ["Nombre"] = "Aceite", ["Descripcion"] = "10W-30",
                ["PrecioUnitario"] = caso == "precio" ? "10.001" : "10.01",
                ["Costo"] = "5.01", ["Stock"] = caso == "stock" ? "10.0001" : "10.001"
            };
            var respuesta = await _cliente.PostAsync(ruta, new FormUrlEncodedContent(datos));
            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
            var html = WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync());
            if (caso == "precio") Assert.Contains(ReglasTaller.ErrorImporte, html);
            if (caso == "stock") Assert.Contains(ReglasTaller.ErrorCantidad, html);
            using var scope = _app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TallerDbContext>();
            var productos = await db.Productos.AsNoTracking().OrderBy(p => p.IdProducto).ToListAsync();
            Assert.Equal(caso == "valido" && nuevo ? 3 : 2, productos.Count);
            if (caso == "valido")
            {
                var guardado = nuevo ? productos[^1] : productos[0];
                Assert.Equal(10.01m, guardado.PrecioUnitario);
                Assert.Equal(10.001m, guardado.Stock);
            }
            else
            {
                Assert.Equal(350m, productos[0].PrecioUnitario);
                Assert.Equal(20m, productos[0].Stock);
            }
        }
    }

    [Fact]
    public async Task Factura_con_servicio_repetido_muestra_error_y_permite_corregir_sin_crear_detalles()
    {
        var id = await AbrirTrabajo();
        using (var scope = _app.Services.CreateScope())
            Assert.True((await scope.ServiceProvider.GetRequiredService<ServicioService>().Finalizar(id, new DateOnly(2026, 9, 2))).EsCorrecto);
        var token = Valor(await _cliente.GetStringAsync("/Facturas/Crear?idCliente=1"), "__RequestVerificationToken");
        foreach (var repetido in new[] { true, false })
        {
            var datos = new List<KeyValuePair<string, string>>
            {
                new("__RequestVerificationToken", token), new("IdCliente", "1"),
                new("MetodoPago", "Efectivo"), new("IdServicios", id.ToString())
            };
            if (repetido) datos.Add(new("IdServicios", id.ToString()));
            var respuesta = await _cliente.PostAsync("/Facturas/Crear", new FormUrlEncodedContent(datos));
            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
            if (repetido) Assert.Contains(ReglasTaller.ErrorServiciosRepetidos, WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync()));
            using var scope = _app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TallerDbContext>();
            Assert.Equal(repetido ? 0 : 1, await db.Facturas.CountAsync());
            Assert.Equal(repetido ? 0 : 1, await db.DetalleFactura.CountAsync());
        }
    }

    [Theory]
    [InlineData("Clientes")]
    [InlineData("Vehiculos")]
    [InlineData("Trabajadores")]
    [InlineData("Productos")]
    public async Task Formulario_real_desactiva_y_reactiva_y_otro_contexto_observa_el_cambio(string modulo)
    {
        for (var ciclo = 0; ciclo < 2; ciclo++)
        {
            var html = await _cliente.GetStringAsync($"/{modulo}");
            var formulario = FormularioEstado(html, modulo);
            Assert.Equal("false", Valor(formulario, "activo"));
            var respuesta = await EnviarEstado(modulo, formulario, "false");
            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
            Assert.False(await ActivoPersistido(modulo));
            formulario = FormularioEstado(await respuesta.Content.ReadAsStringAsync(), modulo);
            Assert.Equal("true", Valor(formulario, "activo"));
            respuesta = await EnviarEstado(modulo, formulario, "true");
            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
            Assert.True(await ActivoPersistido(modulo));
            Assert.Equal("false", Valor(FormularioEstado(await respuesta.Content.ReadAsStringAsync(), modulo), "activo"));
        }
    }

    [Fact]
    public async Task Historial_conserva_vehiculo_seleccionado_y_avisa_si_no_existe()
    {
        var html = await _cliente.GetStringAsync("/Historial?idVehiculo=1");
        var selector = Regex.Match(html, "<select[^>]*id=\"filtro-idvehiculo\"[^>]*>([\\s\\S]*?)</select>").Value;
        Assert.Matches("<option(?=[^>]*value=\"1\")(?=[^>]*selected)[^>]*>", selector);
        Assert.Contains("MGA-1234", html);
        var inexistente = await _cliente.GetStringAsync("/Historial?idVehiculo=2147483647");
        Assert.Contains("El vehículo seleccionado no existe", WebUtility.HtmlDecode(inexistente));
    }

    [Fact]
    public async Task Cambiar_estado_conserva_busqueda_y_pagina_del_listado()
    {
        int id;
        using (var scope = _app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TallerDbContext>();
            var nuevos = Enumerable.Range(1, 12).Select(n => new TallerMecanicoSantoDomingo.Models.Cliente
            {
                Nombre = "Filtro", Apellido = $"Prueba {n:00}", RFC = $"FILTRO-{n}", Telefono = $"7000{n:0000}", Direccion = "Managua"
            }).ToList();
            db.Clientes.AddRange(nuevos);
            await db.SaveChangesAsync();
            id = nuevos[^1].IdCliente;
        }
        var html = await _cliente.GetStringAsync("/Clientes?buscar=Filtro&pagina=2");
        var formulario = FormularioEstado(html, "Clientes", id);
        Assert.Equal("Filtro", Valor(formulario, "buscar"));
        Assert.Equal("2", Valor(formulario, "pagina"));
        var respuesta = await _cliente.PostAsync($"/Clientes/CambiarEstado/{id}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Valor(formulario, "__RequestVerificationToken"), ["activo"] = "false",
            ["buscar"] = Valor(formulario, "buscar"), ["pagina"] = Valor(formulario, "pagina")
        }));
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Contains("buscar=Filtro", respuesta.RequestMessage!.RequestUri!.Query);
        Assert.Contains("pagina=2", respuesta.RequestMessage.RequestUri.Query);
        Assert.Contains("Página 2 de 2", WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync()));
        using var verificacion = _app.Services.CreateScope();
        Assert.False((await verificacion.ServiceProvider.GetRequiredService<TallerDbContext>().Clientes.AsNoTracking().SingleAsync(c => c.IdCliente == id)).Activo);
    }

    [Fact]
    public async Task Ficha_de_trabajo_guia_el_cierre_y_no_permita_cambiar_relaciones_ni_retirar_al_ultimo()
    {
        var id = await AbrirTrabajo();
        var editar = await _cliente.GetStringAsync($"/Servicios/Editar/{id}");
        Assert.Matches("<select(?=[^>]*name=\"IdVehiculo\")(?=[^>]*disabled)[^>]*>", editar);
        Assert.Matches("<select(?=[^>]*name=\"TipoServicio\")(?=[^>]*disabled)[^>]*>", editar);
        Assert.Equal("1", Valor(editar, "IdVehiculo"));
        Assert.Equal("Mantenimiento", Valor(editar, "TipoServicio"));
        var detalle = await _cliente.GetStringAsync($"/Servicios/Detalles/{id}");
        foreach (var ancla in new[] { "personal-servicio", "productos-servicio", "cierre-servicio" })
            Assert.Contains($"id=\"{ancla}\"", detalle);
        Assert.Contains("Trabajo abierto", detalle);
        Assert.Matches("<button(?=[^>]*title=\"Asigne primero un reemplazo habilitado\")(?=[^>]*disabled)[^>]*>", detalle);
        var intento = await _cliente.PostAsync("/Asignaciones/Desasignar", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Valor(detalle, "__RequestVerificationToken"), ["idServicio"] = id.ToString(), ["idTrabajador"] = "1"
        }));
        Assert.Equal(HttpStatusCode.OK, intento.StatusCode);
        using var scope = _app.Services.CreateScope();
        Assert.Equal("Activo", (await scope.ServiceProvider.GetRequiredService<TallerDbContext>().DetalleServicio.AsNoTracking().SingleAsync()).Estado);
    }

    [Fact]
    public async Task Trabajo_finalizado_ofrece_factura_y_muestra_personal_historico_sin_asignar()
    {
        var id = await AbrirTrabajo();
        using (var scope = _app.Services.CreateScope())
            Assert.True((await scope.ServiceProvider.GetRequiredService<ServicioService>().Finalizar(id, new DateOnly(2026, 9, 2))).EsCorrecto);
        var detalle = await _cliente.GetStringAsync($"/Servicios/Detalles/{id}");
        Assert.Contains("Preparar factura", detalle);
        Assert.Contains("/Facturas/Crear?idCliente=1", detalle);
        var asignaciones = await _cliente.GetStringAsync($"/Asignaciones?idServicio={id}");
        Assert.Contains("Este servicio se consulta como historial", asignaciones);
        Assert.DoesNotContain("action=\"/Asignaciones/Asignar\"", asignaciones);
        var listado = WebUtility.HtmlDecode(await _cliente.GetStringAsync("/Servicios"));
        Assert.Contains("Personal del historial", listado);
        Assert.Contains("Pedro Martínez", listado);
        var intentoEdicion = await _cliente.GetAsync($"/Servicios/Editar/{id}");
        Assert.Equal($"/Servicios/Detalles/{id}", intentoEdicion.RequestMessage!.RequestUri!.AbsolutePath);
        using (var scope = _app.Services.CreateScope())
            Assert.True((await scope.ServiceProvider.GetRequiredService<FacturacionService>().CrearFactura(1, "Efectivo", [id], null)).EsCorrecto);
        detalle = await _cliente.GetStringAsync($"/Servicios/Detalles/{id}");
        Assert.Contains("Ver factura FAC-", detalle);
        Assert.DoesNotContain("Preparar factura", detalle);
    }

    private async Task<int> AbrirTrabajo()
    {
        using var scope = _app.Services.CreateScope();
        var resultado = await scope.ServiceProvider.GetRequiredService<ServicioService>().Crear(new()
        {
            IdVehiculo = 1, TipoServicio = "Mantenimiento", Descripcion = "Revisión de frenos", DescripcionFalla = "Ruido al frenar",
            FechaInicio = new DateOnly(2026, 9, 1), Total = 100
        }, 1);
        Assert.True(resultado.EsCorrecto, resultado.Mensaje);
        return resultado.Datos!.IdServicio;
    }

    [Fact]
    public async Task Estado_invalido_ausente_o_sin_antiforgery_no_modifica_el_cliente()
    {
        var formulario = FormularioEstado(await _cliente.GetStringAsync("/Clientes"), "Clientes");
        foreach (var estado in new string?[] { "value", "", null })
        {
            await EnviarEstado("Clientes", formulario, estado);
            Assert.True(await ActivoPersistido("Clientes"));
        }
        var sinToken = await _cliente.PostAsync("/Clientes/CambiarEstado/1", new FormUrlEncodedContent(new Dictionary<string, string> { ["activo"] = "false" }));
        Assert.Equal(HttpStatusCode.BadRequest, sinToken.StatusCode);
        Assert.True(await ActivoPersistido("Clientes"));
    }

    [Fact]
    public async Task Cliente_desactivado_sigue_en_filtros_y_se_conserva_al_editar_su_vehiculo()
    {
        var formulario = FormularioEstado(await _cliente.GetStringAsync("/Clientes"), "Clientes");
        await EnviarEstado("Clientes", formulario, "false");
        var filtro = await _cliente.GetStringAsync("/Vehiculos?idCliente=1");
        Assert.Contains("(desactivado)", filtro);
        var editar = await _cliente.GetStringAsync("/Vehiculos/Editar/1");
        var selector = Regex.Match(editar, "<select[^>]*name=\"IdCliente\"[^>]*>([\\s\\S]*?)</select>").Value;
        Assert.Matches("<option(?=[^>]*value=\"1\")(?=[^>]*selected)[^>]*>", selector);
        var datos = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Valor(editar, "__RequestVerificationToken"),
            ["IdVehiculo"] = "1", ["IdCliente"] = "1", ["Placa"] = "M CORREGIDA1", ["Marca"] = "Toyota",
            ["Modelo"] = "Corolla", ["Anio"] = "2020", ["Kilometraje"] = "85001"
        };
        var respuesta = await _cliente.PostAsync("/Vehiculos/Editar/1", new FormUrlEncodedContent(datos));
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        using var scope = _app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TallerDbContext>();
        var vehiculo = await db.Vehiculos.AsNoTracking().SingleAsync(v => v.IdVehiculo == 1);
        Assert.Equal("M CORREGIDA1", vehiculo.Placa);
        Assert.Equal(1, vehiculo.IdCliente);
        Assert.False(await ActivoPersistido("Clientes"));
        var alta = await scope.ServiceProvider.GetRequiredService<VehiculoService>().Crear(new()
        {
            IdCliente = 1, Placa = "M NUEVO1", Marca = "Toyota", Modelo = "Corolla", Anio = 2020
        });
        Assert.False(alta.EsCorrecto);
        Assert.Contains("desactivado", alta.Mensaje);
    }

    [Fact]
    public async Task Personal_inactivo_o_con_salida_no_aparece_disponible_en_pantallas()
    {
        var formulario = FormularioEstado(await _cliente.GetStringAsync("/Trabajadores"), "Trabajadores");
        var respuesta = await EnviarEstado("Trabajadores", formulario, "false");
        var html = await respuesta.Content.ReadAsStringAsync();
        var fila = Assert.Single(Regex.Matches(html, "<tr\\b[^>]*>[\\s\\S]*?</tr>").Cast<Match>(),
            m => m.Value.Contains("action=\"/Trabajadores/CambiarEstado/1\""));
        Assert.Contains("No disponible", fila.Value);
        Assert.DoesNotContain(">Disponible<", fila.Value);
        var ficha = await _cliente.GetStringAsync("/Trabajadores/Detalles/1");
        Assert.Contains("No disponible: trabajador desactivado", ficha);
        Assert.DoesNotContain("Disponible para nuevos trabajos", ficha);

        formulario = FormularioEstado(html, "Trabajadores");
        await EnviarEstado("Trabajadores", formulario, "true");
        using (var scope = _app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TallerDbContext>();
            var trabajador = await db.Trabajadores.AsNoTracking().SingleAsync(t => t.IdTrabajador == 1);
            trabajador.FechaSalida = DateOnly.FromDateTime(trabajador.FechaRegistro).AddDays(1);
            Assert.True((await scope.ServiceProvider.GetRequiredService<TrabajadorService>().Guardar(trabajador, false)).EsCorrecto);
        }
        html = await _cliente.GetStringAsync("/Trabajadores");
        fila = Assert.Single(Regex.Matches(html, "<tr\\b[^>]*>[\\s\\S]*?</tr>").Cast<Match>(),
            m => m.Value.Contains("action=\"/Trabajadores/CambiarEstado/1\""));
        Assert.Contains("No disponible", fila.Value);
        ficha = await _cliente.GetStringAsync("/Trabajadores/Detalles/1");
        Assert.Contains("No disponible: fecha de salida registrada", ficha);
        Assert.Contains("No disponible", await _cliente.GetStringAsync("/Reportes/Trabajadores"));
    }

    [Fact]
    public async Task Vehiculos_de_clientes_desactivados_no_se_ofrecen_para_trabajos_nuevos()
    {
        var formulario = FormularioEstado(await _cliente.GetStringAsync("/Clientes"), "Clientes");
        await EnviarEstado("Clientes", formulario, "false");
        var html = await _cliente.GetStringAsync("/Servicios/Crear");
        var selector = Regex.Match(html, "<select[^>]*name=\"IdVehiculo\"[^>]*>([\\s\\S]*?)</select>").Value;
        Assert.DoesNotContain("value=\"1\"", selector);
        Assert.DoesNotContain("value=\"2\"", selector);
        Assert.Contains("value=\"3\"", selector);
    }

    private Task<HttpResponseMessage> EnviarEstado(string modulo, string formulario, string? estado)
    {
        var datos = new Dictionary<string, string> { ["__RequestVerificationToken"] = Valor(formulario, "__RequestVerificationToken") };
        if (estado is not null) datos["activo"] = estado;
        return _cliente.PostAsync($"/{modulo}/CambiarEstado/1", new FormUrlEncodedContent(datos));
    }

    private async Task<bool> ActivoPersistido(string modulo)
    {
        using var scope = _app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TallerDbContext>();
        return modulo switch
        {
            "Clientes" => await db.Clientes.AsNoTracking().Where(c => c.IdCliente == 1).Select(c => c.Activo).SingleAsync(),
            "Vehiculos" => await db.Vehiculos.AsNoTracking().Where(c => c.IdVehiculo == 1).Select(c => c.Activo).SingleAsync(),
            "Trabajadores" => await db.Trabajadores.AsNoTracking().Where(c => c.IdTrabajador == 1).Select(c => c.Activo).SingleAsync(),
            _ => await db.Productos.AsNoTracking().Where(c => c.IdProducto == 1).Select(c => c.Activo).SingleAsync()
        };
    }

    private static string FormularioEstado(string html, string modulo, int id = 1) => Assert.Single(
        Regex.Matches(html, "<form\\b[^>]*>[\\s\\S]*?</form>").Cast<Match>(),
        m => m.Value.Contains($"action=\"/{modulo}/CambiarEstado/{id}\"")).Value;

    private static string Valor(string html, string nombre)
    {
        var entrada = Regex.Matches(html, "<input\\b[^>]*>").Cast<Match>()
            .First(m => m.Value.Contains($"name=\"{nombre}\""));
        return WebUtility.HtmlDecode(Regex.Match(entrada.Value, "\\bvalue=\"([^\"]*)\"").Groups[1].Value);
    }

    private sealed class AccesoPruebas(IOptionsMonitor<AuthenticationSchemeOptions> opciones, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(opciones, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "operador-pruebas"), new Claim(ClaimTypes.Name, "Pruebas")], Scheme.Name)), Scheme.Name)));
    }
}
