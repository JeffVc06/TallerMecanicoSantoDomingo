using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TallerMecanicoSantoDomingo.Models;
using TallerMecanicoSantoDomingo.Services;
using TallerMecanicoSantoDomingo.ViewModels;

namespace TallerMecanicoSantoDomingo.Controllers;

/// <summary>Catálogo de productos (MER: REPUESTO; Reglas 10, 14 y relación UTILIZA).</summary>
[Authorize]
public class ProductosController : Controller
{
    private readonly ProductoService _productos;

    public ProductosController(ProductoService productos) => _productos = productos;

    [HttpGet]
    public async Task<IActionResult> Index(string? buscar, int pagina = 1)
    {
        var resultado = await _productos.Buscar(buscar, null, pagina, 10);
        ViewBag.Buscar = buscar;
        return View(resultado);
    }

    [HttpGet]
    public IActionResult Crear() => View(new ProductoFormViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(ProductoFormViewModel modelo)
    {
        if (!ModelState.IsValid)
            return View(modelo);

        var resultado = await _productos.Crear(new Producto
        {
            Nombre = modelo.Nombre,
            Descripcion = modelo.Descripcion,
            PrecioUnitario = modelo.PrecioUnitario,
            Stock = modelo.Stock
        });

        if (!resultado.EsCorrecto)
        {
            ModelState.AddModelError(string.Empty, resultado.Mensaje ?? "No se pudo registrar el producto.");
            return View(modelo);
        }

        TempData["Exito"] = resultado.Mensaje;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Editar(int id)
    {
        var producto = await _productos.ObtenerPorId(id);
        if (producto is null)
            return RedirectToAction(nameof(Index));

        return View(new ProductoFormViewModel
        {
            IdProducto = producto.IdProducto,
            Nombre = producto.Nombre,
            Descripcion = producto.Descripcion,
            PrecioUnitario = producto.PrecioUnitario,
            Stock = producto.Stock
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(ProductoFormViewModel modelo)
    {
        if (!ModelState.IsValid)
            return View(modelo);

        var resultado = await _productos.Editar(new Producto
        {
            IdProducto = modelo.IdProducto,
            Nombre = modelo.Nombre,
            Descripcion = modelo.Descripcion,
            PrecioUnitario = modelo.PrecioUnitario,
            Stock = modelo.Stock
        });

        if (!resultado.EsCorrecto)
        {
            ModelState.AddModelError(string.Empty, resultado.Mensaje ?? "No se pudo actualizar el producto.");
            return View(modelo);
        }

        TempData["Exito"] = resultado.Mensaje;
        return RedirectToAction(nameof(Detalles), new { id = modelo.IdProducto });
    }

    /// <summary>Consulta del producto y su historial de utilización en servicios.</summary>
    [HttpGet]
    public async Task<IActionResult> Detalles(int id)
    {
        var producto = await _productos.ObtenerPorId(id);
        if (producto is null)
            return RedirectToAction(nameof(Index));

        ViewBag.Historial = await _productos.HistorialDelProducto(id);
        return View(producto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEstado(int id, bool activo)
    {
        var resultado = await _productos.CambiarEstado(id, activo);
        TempData[resultado.EsCorrecto ? "Exito" : "Error"] = resultado.Mensaje;
        return RedirectToAction(nameof(Index));
    }
}
