using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TallerMecanicoSantoDomingo.Data;
using TallerMecanicoSantoDomingo.Models;
using TallerMecanicoSantoDomingo.Services;
using TallerMecanicoSantoDomingo.ViewModels;

namespace TallerMecanicoSantoDomingo.Controllers;

/// <summary>Gestión de trabajadores con carga real y disponibilidad (Reglas 5 y 7).</summary>
[Authorize]
public class TrabajadoresController : Controller
{
    private readonly TrabajadorService _trabajadores;
    private readonly TallerDbContext _db;
    private readonly ServicioService _servicios;
    private readonly AsignacionService _asignaciones;

    public TrabajadoresController(TallerDbContext db, ServicioService servicios, AsignacionService asignaciones, TrabajadorService trabajadores)
    {
        _db = db;
        _trabajadores = trabajadores;
        _servicios = servicios;
        _asignaciones = asignaciones;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? buscar)
    {
        var query = _db.Trabajadores.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var t = buscar.Trim();
            query = query.Where(x => x.Nombre.Contains(t) || x.Apellido.Contains(t) || x.Telefono.Contains(t));
        }

        var trabajadores = await query.OrderBy(t => t.Apellido).ThenBy(t => t.Nombre).ToListAsync();
        var cargas = await _servicios.CargasDeTrabajadores(trabajadores.Select(t => t.IdTrabajador).ToList());

        ViewBag.Buscar = buscar;
        ViewBag.Cargas = cargas.ToDictionary(c => c.IdTrabajador);
        return View(trabajadores);
    }

    [HttpGet]
    public IActionResult Crear()
    {
        CargarAreas();
        return View(new TrabajadorFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(TrabajadorFormViewModel modelo)
    {
        if (!ModelState.IsValid)
        {
            CargarAreas();
            return View(modelo);
        }

        var resultado = await _trabajadores.Guardar(Mapear(modelo), nuevo: true);
        if (!resultado.EsCorrecto)
        {
            ModelState.AddModelError(string.Empty, resultado.Mensaje!);
            CargarAreas();
            return View(modelo);
        }
        TempData["Exito"] = resultado.Mensaje;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Editar(int id)
    {
        var trabajador = await _db.Trabajadores.FindAsync(id);
        if (trabajador is null)
            return RedirectToAction(nameof(Index));

        CargarAreas();
        return View(new TrabajadorFormViewModel
        {
            IdTrabajador = trabajador.IdTrabajador,
            Nombre = trabajador.Nombre,
            Apellido = trabajador.Apellido,
            Telefono = trabajador.Telefono,
            Area = trabajador.Area,
            Correo = trabajador.Correo,
            FechaIngreso = DateOnly.FromDateTime(trabajador.FechaRegistro),
            FechaSalida = trabajador.FechaSalida
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(TrabajadorFormViewModel modelo)
    {
        if (!ModelState.IsValid)
        {
            CargarAreas();
            return View(modelo);
        }

        var resultado = await _trabajadores.Guardar(Mapear(modelo), nuevo: false);
        if (!resultado.EsCorrecto)
        {
            ModelState.AddModelError(string.Empty, resultado.Mensaje!);
            CargarAreas();
            return View(modelo);
        }
        TempData["Exito"] = resultado.Mensaje;
        return RedirectToAction(nameof(Detalles), new { id = modelo.IdTrabajador });
    }

    /// <summary>Ficha con historial completo de asignaciones y carga actual.</summary>
    [HttpGet]
    public async Task<IActionResult> Detalles(int id)
    {
        var trabajador = await _db.Trabajadores.AsNoTracking()
            .FirstOrDefaultAsync(t => t.IdTrabajador == id);
        if (trabajador is null)
            return RedirectToAction(nameof(Index));

        ViewBag.Asignaciones = await _asignaciones.AsignacionesDelTrabajador(id);
        var carga = (await _servicios.CargasDeTrabajadores(new List<int> { id })).FirstOrDefault();
        ViewBag.Carga = carga;
        return View(trabajador);
    }

    /// <summary>Regla 14: desactivación lógica; el historial de asignaciones se conserva.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEstado(int id, [Microsoft.AspNetCore.Mvc.ModelBinding.BindRequired] bool activo)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "El estado o el identificador recibido no es válido.";
            return RedirectToAction(nameof(Index));
        }
        var resultado = await _trabajadores.CambiarEstado(id, activo);
        TempData[resultado.EsCorrecto ? "Exito" : "Error"] = resultado.Mensaje;
        return RedirectToAction(nameof(Index));
    }

    private static Trabajador Mapear(TrabajadorFormViewModel modelo) => new()
    {
        IdTrabajador = modelo.IdTrabajador, Nombre = modelo.Nombre, Apellido = modelo.Apellido,
        Telefono = modelo.Telefono, Area = modelo.Area, Correo = modelo.Correo,
        FechaRegistro = modelo.FechaIngreso!.Value.ToDateTime(TimeOnly.MinValue), FechaSalida = modelo.FechaSalida
    };

    private void CargarAreas() => ViewBag.Areas = new List<SelectListItem>
    {
        new("Mantenimiento mecánico", "Mantenimiento mecánico"),
        new("Pintura", "Pintura")
    };
}
