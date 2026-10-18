using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TallerMecanicoSantoDomingo.ViewModels;

namespace TallerMecanicoSantoDomingo.Controllers;

/// <summary>
/// Controlador de acceso: inicio de sesión, cierre de sesión y configuración
/// inicial del primer administrador (habilitada solo cuando no existe ningún usuario).
/// </summary>
[Authorize]
public class AccountController : Controller
{
    private readonly SignInManager<IdentityUser> _ingreso;
    private readonly UserManager<IdentityUser> _gestor;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        SignInManager<IdentityUser> ingreso,
        UserManager<IdentityUser> gestor,
        ILogger<AccountController> logger)
    {
        _ingreso = ingreso;
        _gestor = gestor;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Dashboard");

        ViewBag.PrimeraConfiguracion = !await _gestor.Users.AnyAsync();
        return View(new LoginViewModel { UrlRetorno = returnUrl });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        ViewBag.PrimeraConfiguracion = !await _gestor.Users.AnyAsync();

        if (!ModelState.IsValid)
            return View(model);

        var usuario = await _gestor.FindByEmailAsync(model.Email)
                      ?? await _gestor.FindByNameAsync(model.Email);

        if (usuario is null)
        {
            ModelState.AddModelError(string.Empty, "Credenciales incorrectas. Verifique su usuario y contraseña.");
            return View(model);
        }

        var resultado = await _ingreso.PasswordSignInAsync(usuario, model.Password, model.Recordarme, lockoutOnFailure: true);

        if (resultado.Succeeded)
        {
            _logger.LogInformation("Inicio de sesión correcto para {Email}.", usuario.Email);
            if (!string.IsNullOrEmpty(model.UrlRetorno) && Url.IsLocalUrl(model.UrlRetorno))
                return Redirect(model.UrlRetorno);
            return RedirectToAction("Index", "Dashboard");
        }

        if (resultado.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "La cuenta está bloqueada por demasiados intentos fallidos. Espere 15 minutos e intente nuevamente.");
            return View(model);
        }

        ModelState.AddModelError(string.Empty, "Credenciales incorrectas. Verifique su usuario y contraseña.");
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _ingreso.SignOutAsync();
        _logger.LogInformation("Cierre de sesión realizado.");
        return RedirectToAction("Login");
    }

    /// <summary>
    /// Permite crear el primer administrador únicamente mientras no exista
    /// ningún usuario registrado (uso único, sin datos predeterminados inseguros).
    /// </summary>
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> ConfiguracionInicial()
    {
        if (await _gestor.Users.AnyAsync())
            return RedirectToAction(nameof(Login));
        return View(new ConfiguracionInicialViewModel());
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfiguracionInicial(ConfiguracionInicialViewModel model)
    {
        if (await _gestor.Users.AnyAsync())
            return RedirectToAction(nameof(Login));

        if (!ModelState.IsValid)
            return View(model);

        var usuario = new IdentityUser
        {
            UserName = model.Email.Trim(),
            Email = model.Email.Trim(),
            EmailConfirmed = true
        };

        var resultado = await _gestor.CreateAsync(usuario, model.Password);
        if (!resultado.Succeeded)
        {
            foreach (var error in resultado.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        _logger.LogInformation("Primer administrador creado mediante la configuración inicial.");
        TempData["Exito"] = "Administrador creado correctamente. Inicie sesión para continuar.";
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult AccesoDenegado()
    {
        return View();
    }

    /// <summary>Página de error controlada; no expone detalles internos.</summary>
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Error()
    {
        return View(new ErrorViewModel
        {
            Mensaje = "Ocurrió un error inesperado al procesar la solicitud.",
            Detalle = "El incidente fue registrado. Intente nuevamente o contacte al administrador del sistema."
        });
    }
}
