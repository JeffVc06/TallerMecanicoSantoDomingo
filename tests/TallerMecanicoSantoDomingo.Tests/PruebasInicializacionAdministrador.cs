using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TallerMecanicoSantoDomingo.Data;
using Xunit;

namespace TallerMecanicoSantoDomingo.Tests;

public class PruebasInicializacionAdministrador
{
    [Fact]
    public async Task Semilla_de_administrador_usa_identity_y_guarda_la_contrasena()
    {
        var servicios = new ServiceCollection();
        servicios.AddLogging();
        servicios.AddDbContext<TallerDbContext>(opciones =>
            opciones.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        servicios.AddIdentityCore<IdentityUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<TallerDbContext>();

        using var proveedor = servicios.BuildServiceProvider();
        using var alcance = proveedor.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<TallerDbContext>();
        await db.Database.EnsureCreatedAsync();
        var gestor = alcance.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AdministradorInicial:Email"] = "admin@taller.local",
                ["AdministradorInicial:Password"] = "TallerSeguro123!"
            })
            .Build();

        await DatabaseInitializer.SemillarAdministradorInicial(
            db, gestor, configuracion, NullLogger.Instance);

        var administrador = await db.Users.SingleAsync();
        Assert.Equal("admin@taller.local", administrador.Email);
        Assert.True((await gestor.CheckPasswordAsync(administrador, "TallerSeguro123!")));
    }
}
