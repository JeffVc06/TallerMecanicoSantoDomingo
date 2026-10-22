using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TallerMecanicoSantoDomingo.Data;
using TallerMecanicoSantoDomingo.Models;
using TallerMecanicoSantoDomingo.Services;
using TallerMecanicoSantoDomingo.ViewModels;

namespace TallerMecanicoSantoDomingo.Controllers;

/// <summary>
/// Módulo de facturación (MER: FACTURA y DETALLE_FACTURA; Reglas 11, 12, 13 y 14).
/// Todos los cálculos de subtotal, IVA y total se realizan en el servidor.
/// </summary>
[Authorize]
public class FacturasController : Controller
{
    private readonly FacturacionService _facturas;
    private readonly TallerDbContext _db;
    private readonly ConfiguracionTaller _config;

    public FacturasController(FacturacionService facturas, TallerDbContext db, Microsoft.Extensions.Options.IOptions<ConfiguracionTaller> config)
    {
        _facturas = facturas;
        _db = db;
        _config = config.Value;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? buscar, DateOnly? desde, DateOnly? hasta, int pagina = 1)
    {
        var resultado = await _facturas.Buscar(buscar, null, desde, hasta, pagina, 10);
        ViewBag.Buscar = buscar;
        ViewBag.Desde = desde;
        ViewBag.Hasta = hasta;
        return View(resultado);
    }

    [HttpGet]
    public async Task<IActionResult> Crear(int? idCliente = null)
    {
        await CargarClientes();
        var modelo = new FacturaCrearViewModel { IdCliente = idCliente ?? 0, PorcentajeIVA = _config.PorcentajeIVA };
        if (idCliente.HasValue)
            ViewBag.Elegibles = await _facturas.ServiciosElegibles(idCliente.Value);
        return View(modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(FacturaCrearViewModel modelo)
    {
        modelo.PorcentajeIVA = _config.PorcentajeIVA;

        if (!ModelState.IsValid)
        {
            await CargarClientes();
            ViewBag.Elegibles = await _facturas.ServiciosElegibles(modelo.IdCliente);
            return View(modelo);
        }

        var resultado = await _facturas.CrearFactura(modelo.IdCliente, modelo.MetodoPago, modelo.IdServicios, modelo.Notas);
        if (!resultado.EsCorrecto)
        {
            ModelState.AddModelError(string.Empty, resultado.Mensaje ?? "No se pudo crear la factura.");
            await CargarClientes();
            ViewBag.Elegibles = await _facturas.ServiciosElegibles(modelo.IdCliente);
            return View(modelo);
        }

        TempData["Exito"] = resultado.Mensaje;
        return RedirectToAction(nameof(Imprimir), new { id = resultado.Datos!.IdFactura });
    }

    /// <summary>Servicios elegibles del cliente elegido (consultados al vuelo por el formulario).</summary>
    [HttpGet]
    public async Task<IActionResult> ServiciosElegibles(int idCliente)
    {
        var elegibles = await _facturas.ServiciosElegibles(idCliente);
        return Json(new
        {
            servicios = elegibles.Select(e => new
            {
                e.IdServicio, e.Placa, e.TipoServicio, e.Descripcion,
                FechaInicio = e.FechaInicio.ToString("dd/MM/yyyy"),
                Estado = e.Estado,
                Total = e.Total.ToString("0.00")
            }),
            porcentajeIVA = _config.PorcentajeIVA.ToString("0.##")
        });
    }

    [HttpGet]
    public async Task<IActionResult> Detalles(int id)
    {
        var completa = await _facturas.ObtenerParaImpresion(id);
        if (completa is null)
            return RedirectToAction(nameof(Index));
        return View(completa);
    }

    /// <summary>Vista imprimible de la factura (imprimir o guardar en PDF desde el navegador).</summary>
    [HttpGet]
    public async Task<IActionResult> Imprimir(int id)
    {
        var completa = await _facturas.ObtenerParaImpresion(id);
        if (completa is null)
            return RedirectToAction(nameof(Index));

        ViewBag.NombreTaller = _config.NombreTaller;
        ViewBag.DireccionTaller = _config.DireccionTaller;
        ViewBag.TelefonoTaller = _config.TelefonoTaller;
        ViewBag.PorcentajeIVA = _config.PorcentajeIVA;
        return View(completa);
    }

    /// <summary>Regla 14: anulación controlada que preserva la trazabilidad.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Anular(int id, string? motivo)
    {
        var resultado = await _facturas.AnularFactura(id, motivo);
        TempData[resultado.EsCorrecto ? "Exito" : "Error"] = resultado.Mensaje;
        return RedirectToAction(nameof(Detalles), new { id });
    }

    private async Task CargarClientes()
    {
        var clientes = await _db.Clientes.AsNoTracking()
            .Where(c => c.Activo)
            .OrderBy(c => c.Apellido).ThenBy(c => c.Nombre)
            .Select(c => new SelectListItem($"{c.Nombre} {c.Apellido} — {c.RFC}", c.IdCliente.ToString()))
            .ToListAsync();
        ViewBag.Clientes = clientes;
    }
}
