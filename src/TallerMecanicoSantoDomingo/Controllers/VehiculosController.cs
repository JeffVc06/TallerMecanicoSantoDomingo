using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TallerMecanicoSantoDomingo.Data;
using TallerMecanicoSantoDomingo.Models;
using TallerMecanicoSantoDomingo.Services;
using TallerMecanicoSantoDomingo.ViewModels;

namespace TallerMecanicoSantoDomingo.Controllers;

/// <summary>CRUD de vehículos y acceso al historial (Reglas 2, 3 y 14).</summary>
[Authorize]
public class VehiculosController : Controller
{
    private readonly IVehiculoService _vehiculos;
    private readonly TallerDbContext _db;

    public VehiculosController(IVehiculoService vehiculos, TallerDbContext db)
    {
        _vehiculos = vehiculos;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? buscar, int? idCliente, int pagina = 1)
    {
        var resultado = await _vehiculos.Buscar(buscar, idCliente, null, pagina, 10);
        ViewBag.Buscar = buscar;
        ViewBag.IdCliente = idCliente;
        ViewBag.Clientes = await ListaDeClientes();
        return View(resultado);
    }

    [HttpGet]
    public async Task<IActionResult> Crear(int? idCliente = null)
    {
        ViewBag.Clientes = await ListaDeClientes();
        return View(new VehiculoFormViewModel
        {
            IdCliente = idCliente ?? 0,
            Anio = DateTime.Now.Year
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear([Bind(Prefix = "")] VehiculoFormViewModel modelo)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Clientes = await ListaDeClientes();
            return View(modelo);
        }

        var resultado = await _vehiculos.Crear(new Vehiculo
        {
            IdCliente = modelo.IdCliente,
            Placa = modelo.Placa,
            Marca = modelo.Marca,
            Modelo = modelo.Modelo,
            Anio = modelo.Anio
        });

        if (!resultado.EsCorrecto)
        {
            ModelState.AddModelError(string.Empty, resultado.Mensaje ?? "No se pudo registrar el vehículo.");
            ViewBag.Clientes = await ListaDeClientes();
            return View(modelo);
        }

        TempData["Exito"] = resultado.Mensaje;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Editar(int id)
    {
        var vehiculo = await _vehiculos.ObtenerPorId(id);
        if (vehiculo is null)
            return RedirectToAction(nameof(Index));

        ViewBag.Clientes = await ListaDeClientes();
        return View(new VehiculoFormViewModel
        {
            IdVehiculo = vehiculo.IdVehiculo,
            IdCliente = vehiculo.IdCliente,
            Placa = vehiculo.Placa,
            Marca = vehiculo.Marca,
            Modelo = vehiculo.Modelo,
            Anio = vehiculo.Anio
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(VehiculoFormViewModel modelo)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Clientes = await ListaDeClientes();
            return View(modelo);
        }

        var resultado = await _vehiculos.Editar(new Vehiculo
        {
            IdVehiculo = modelo.IdVehiculo,
            IdCliente = modelo.IdCliente,
            Placa = modelo.Placa,
            Marca = modelo.Marca,
            Modelo = modelo.Modelo,
            Anio = modelo.Anio
        });

        if (!resultado.EsCorrecto)
        {
            ModelState.AddModelError(string.Empty, resultado.Mensaje ?? "No se pudo actualizar el vehículo.");
            ViewBag.Clientes = await ListaDeClientes();
            return View(modelo);
        }

        TempData["Exito"] = resultado.Mensaje;
        return RedirectToAction(nameof(Detalles), new { id = modelo.IdVehiculo });
    }

    [HttpGet]
    public async Task<IActionResult> Detalles(int id)
    {
        var vehiculo = await _vehiculos.ObtenerPorId(id);
        if (vehiculo is null)
            return RedirectToAction(nameof(Index));

        ViewBag.Historial = await _vehiculos.ObtenerHistorial(id);
        return View(vehiculo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEstado(int id, bool activo)
    {
        var resultado = await _vehiculos.CambiarEstado(id, activo);
        TempData[resultado.EsCorrecto ? "Exito" : "Error"] = resultado.Mensaje;
        return RedirectToAction(nameof(Index));
    }

    private async Task<List<SelectListItem>> ListaDeClientes()
    {
        var clientes = await _db.Clientes.AsNoTracking()
            .Where(c => c.Activo)
            .OrderBy(c => c.Apellido).ThenBy(c => c.Nombre)
            .Select(c => new SelectListItem($"{c.Nombre} {c.Apellido} — {c.RFC}", c.IdCliente.ToString()))
            .ToListAsync();
        return clientes;
    }
}
