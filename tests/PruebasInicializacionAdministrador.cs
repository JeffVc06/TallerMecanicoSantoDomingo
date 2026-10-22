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
            .AddUserStore<UsuarioStore>();

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

    [Fact]
    public void Base_vacia_necesita_las_tablas()
    {
        using var db = FabricaDeContextos.EnMemoria();
        Assert.True(DatabaseInitializer.NecesitaEsquema(db, []));
    }

    [Fact]
    public void Base_preparada_no_vuelve_a_ejecutar_create_table()
    {
        using var db = FabricaDeContextos.EnMemoria();
        var tablas = db.Model.GetEntityTypes().Select(e => e.GetTableName()!);
        Assert.False(DatabaseInitializer.NecesitaEsquema(db, tablas));
    }

    [Theory]
    [InlineData("Clientes")]
    [InlineData("Cliente")]
    public void Base_anterior_o_incompleta_no_se_modifica(string tabla)
    {
        using var db = FabricaDeContextos.EnMemoria();
        Assert.Throws<InvalidOperationException>(() => DatabaseInitializer.NecesitaEsquema(db, [tabla]));
    }

}
