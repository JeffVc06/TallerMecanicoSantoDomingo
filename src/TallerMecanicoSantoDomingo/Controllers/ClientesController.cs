using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TallerMecanicoSantoDomingo.Models;
using TallerMecanicoSantoDomingo.Services;
using TallerMecanicoSantoDomingo.ViewModels;

namespace TallerMecanicoSantoDomingo.Controllers;

/// <summary>CRUD de clientes (Reglas 1, 2 y 14).</summary>
[Authorize]
public class ClientesController : Controller
{
    private readonly IClienteService _clientes;

    public ClientesController(IClienteService clientes) => _clientes = clientes;

    [HttpGet]
    public async Task<IActionResult> Index(string? buscar, int pagina = 1)
    {
        var resultado = await _clientes.Buscar(buscar, null, pagina, 10);
        ViewBag.Buscar = buscar;
        return View(resultado);
    }

    [HttpGet]
    public IActionResult Crear() => View(new ClienteFormViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(ClienteFormViewModel modelo)
    {
        if (!ModelState.IsValid)
            return View(modelo);

        var resultado = await _clientes.Crear(new Cliente
        {
            Nombre = modelo.Nombre,
            Apellido = modelo.Apellido,
            RFC = modelo.RFC,
            Telefono = modelo.Telefono,
            Direccion = modelo.Direccion
        });

        if (!resultado.EsCorrecto)
        {
            ModelState.AddModelError(string.Empty, resultado.Mensaje ?? "No se pudo registrar el cliente.");
            return View(modelo);
        }

        TempData["Exito"] = resultado.Mensaje;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Editar(int id)
    {
        var cliente = await _clientes.ObtenerPorId(id);
        if (cliente is null)
            return RedirectToAction(nameof(Index));

        return View(new ClienteFormViewModel
        {
            IdCliente = cliente.IdCliente,
            Nombre = cliente.Nombre,
            Apellido = cliente.Apellido,
            RFC = cliente.RFC,
            Telefono = cliente.Telefono,
            Direccion = cliente.Direccion
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(ClienteFormViewModel modelo)
    {
        if (!ModelState.IsValid)
            return View(modelo);

        var resultado = await _clientes.Editar(new Cliente
        {
            IdCliente = modelo.IdCliente,
            Nombre = modelo.Nombre,
            Apellido = modelo.Apellido,
            RFC = modelo.RFC,
            Telefono = modelo.Telefono,
            Direccion = modelo.Direccion
        });

        if (!resultado.EsCorrecto)
        {
            ModelState.AddModelError(string.Empty, resultado.Mensaje ?? "No se pudo actualizar el cliente.");
            return View(modelo);
        }

        TempData["Exito"] = resultado.Mensaje;
        return RedirectToAction(nameof(Detalles), new { id = modelo.IdCliente });
    }

    /// <summary>Ficha del cliente: vehículos, facturas y total facturado.</summary>
    [HttpGet]
    public async Task<IActionResult> Detalles(int id)
    {
        var cliente = await _clientes.ObtenerPorId(id);
        if (cliente is null)
            return RedirectToAction(nameof(Index));

        ViewBag.Vehiculos = await _clientes.ObtenerVehiculosDelCliente(id);
        var total = await _clientes.TotalFacturadoDelCliente(id);
        ViewBag.TotalFacturado = total.Datos;
        return View(cliente);
    }

    /// <summary>Regla 14: desactivación lógica, sin pérdida del historial.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEstado(int id, bool activo)
    {
        var resultado = await _clientes.CambiarEstado(id, activo);
        TempData[resultado.EsCorrecto ? "Exito" : "Error"] = resultado.Mensaje;
        return RedirectToAction(nameof(Index));
    }
}
