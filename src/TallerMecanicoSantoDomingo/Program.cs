using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TallerMecanicoSantoDomingo.Data;
using TallerMecanicoSantoDomingo.Services;

var builder = WebApplication.CreateBuilder(args);

// ===================== BASE DE DATOS (SQL SERVER) =====================
builder.Services.AddDbContext<TallerDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("TallerDB"),
        sql =>
        {
            sql.MigrationsHistoryTable("__HistorialMigraciones", "dbo");
            sql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null);
        }));

// ===================== CONFIGURACIÓN DEL TALLER =====================
builder.Services.Configure<ConfiguracionTaller>(
    builder.Configuration.GetSection("ConfiguracionTaller"));

// ===================== SEGURIDAD: IDENTITY =====================
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
    {
        // Políticas de contraseña.
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;

        // Bloqueo por intentos fallidos.
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;

        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<TallerDbContext>()
    .AddDefaultTokenProviders();

// Cookie de autenticación segura.
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

// Token antiforgery disponible también por encabezado para las peticiones AJAX.
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");

// Todas las páginas privadas exigen autenticación (el login es la única excepción).
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// ===================== SERVICIOS DE NEGOCIO =====================
builder.Services.AddScoped<IClienteService, ClienteService>();
builder.Services.AddScoped<IVehiculoService, VehiculoService>();
builder.Services.AddScoped<IServicioService, ServicioService>();
builder.Services.AddScoped<IAsignacionService, AsignacionService>();
builder.Services.AddScoped<IProductoService, ProductoService>();
builder.Services.AddScoped<IFacturacionService, FacturacionService>();
builder.Services.AddScoped<IReporteService, ReporteService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();

builder.Services.AddControllersWithViews(options =>
{
    // Protección CSRF en todas las peticiones POST de formularios.
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

var app = builder.Build();

// ===================== INICIALIZACIÓN DE LA BASE DE DATOS =====================
// El script database/TallerMecanicoSantoDomingo.sql (fuente única del esquema)
// se aplica automáticamente si la base de datos aún no existe.
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
