using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TallerMecanicoSantoDomingo.Data;
using TallerMecanicoSantoDomingo.Services;

var builder = WebApplication.CreateBuilder(args);

// Base de datos
builder.Services.AddDbContext<TallerDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("TallerDB"),
        sql => sql.CommandTimeout(30)));

// Datos del taller
builder.Services.Configure<ConfiguracionTaller>(
    builder.Configuration.GetSection("ConfiguracionTaller"));

// Inicio de sesión y políticas de contraseña
builder.Services.AddIdentityCore<IdentityUser>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;

        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;

        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddUserStore<UsuarioStore>()
    .AddSignInManager();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
    options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
    options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
}).AddIdentityCookies();

// Duración y opciones de la sesión autenticada.
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccesoDenegado";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

// Las peticiones AJAX envían el token antiforgery en este encabezado.
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");

// Exigir inicio de sesión en todas las páginas salvo las marcadas como públicas.
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// Servicios de negocio
builder.Services.AddScoped<ClienteService>();
builder.Services.AddScoped<VehiculoService>();
builder.Services.AddScoped<ServicioService>();
builder.Services.AddScoped<AsignacionService>();
builder.Services.AddScoped<ProductoService>();
builder.Services.AddScoped<FacturacionService>();
builder.Services.AddScoped<ReporteService>();
builder.Services.AddScoped<DashboardService>();

builder.Services.AddControllersWithViews(options =>
{
    // Validar el token antiforgery en los formularios POST.
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

var app = builder.Build();

// Crear la base y aplicar el esquema al primer arranque.
try
{
    DatabaseInitializer.Inicializar(app.Services);
}
catch (Exception ex)
{
    var log = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Program");
    log.LogCritical(ex,
        "No se pudo inicializar la base de datos. Verifique que SQL Server esté en ejecución y que la cadena " +
        "de conexión 'TallerDB' en appsettings.json (o la variable de entorno ConnectionStrings__TallerDB) sea correcta.");
    throw;
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Account/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

app.Run();
