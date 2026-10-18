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
    private readonly TallerDbContext _db;
    private readonly IServicioService _servicios;
    private readonly IAsignacionService _asignaciones;

    public TrabajadoresController(TallerDbContext db, IServicioService servicios, IAsignacionService asignaciones)
    {
        _db = db;
        _servicios = servicios;
        _asignaciones = asignaciones;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? buscar)
    {
        var query = _db.Trabajadores.AsNoTracking().Include(t => t.Area).AsQueryable();
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
    public async Task<IActionResult> Crear()
    {
        await CargarAreas();
        return View(new TrabajadorFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(TrabajadorFormViewModel modelo)
    {
        if (!ModelState.IsValid)
        {
            await CargarAreas();
            return View(modelo);
        }

        var areaExiste = await _db.Areas.AnyAsync(a => a.IdArea == modelo.IdArea);
        if (!areaExiste)
        {
            ModelState.AddModelError(nameof(modelo.IdArea), "El área de trabajo seleccionada no existe.");
            await CargarAreas();
            return View(modelo);
        }

        _db.Trabajadores.Add(new Trabajador
        {
            Nombre = modelo.Nombre.Trim(),
            Apellido = modelo.Apellido.Trim(),
            Telefono = modelo.Telefono.Trim(),
            IdArea = modelo.IdArea
        });
        await _db.SaveChangesAsync();

        TempData["Exito"] = "Trabajador registrado correctamente.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Editar(int id)
    {
        var trabajador = await _db.Trabajadores.FindAsync(id);
        if (trabajador is null)
            return RedirectToAction(nameof(Index));

        await CargarAreas();
        return View(new TrabajadorFormViewModel
        {
            IdTrabajador = trabajador.IdTrabajador,
            Nombre = trabajador.Nombre,
            Apellido = trabajador.Apellido,
            Telefono = trabajador.Telefono,
            IdArea = trabajador.IdArea
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(TrabajadorFormViewModel modelo)
    {
        if (!ModelState.IsValid)
        {
            await CargarAreas();
            return View(modelo);
        }

        var trabajador = await _db.Trabajadores.FindAsync(modelo.IdTrabajador);
        if (trabajador is null)
            return RedirectToAction(nameof(Index));

        var areaExiste = await _db.Areas.AnyAsync(a => a.IdArea == modelo.IdArea);
        if (!areaExiste)
        {
            ModelState.AddModelError(nameof(modelo.IdArea), "El área de trabajo seleccionada no existe.");
            await CargarAreas();
            return View(modelo);
        }

        trabajador.Nombre = modelo.Nombre.Trim();
        trabajador.Apellido = modelo.Apellido.Trim();
        trabajador.Telefono = modelo.Telefono.Trim();
        trabajador.IdArea = modelo.IdArea;
        await _db.SaveChangesAsync();

        TempData["Exito"] = "Trabajador actualizado correctamente.";
        return RedirectToAction(nameof(Detalles), new { id = modelo.IdTrabajador });
    }

    /// <summary>Ficha con historial completo de asignaciones y carga actual.</summary>
    [HttpGet]
    public async Task<IActionResult> Detalles(int id)
    {
        var trabajador = await _db.Trabajadores.AsNoTracking()
            .Include(t => t.Area)
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
    public async Task<IActionResult> CambiarEstado(int id, bool activo)
    {
        var trabajador = await _db.Trabajadores.FindAsync(id);
        if (trabajador is null)
        {
            TempData["Error"] = "El trabajador indicado no existe.";
            return RedirectToAction(nameof(Index));
        }

        if (!activo)
        {
            var tieneActivos = await _db.DetalleServicio.AnyAsync(d =>
                d.IdTrabajador == id && d.Estado == ValoresCatalogo.AsignacionActiva &&
                d.Servicio!.Estado == ValoresCatalogo.ServicioAbierto);
            if (tieneActivos)
            {
                TempData["Error"] = "No se puede desactivar el trabajador porque tiene servicios activos.";
                return RedirectToAction(nameof(Index));
            }
        }

        trabajador.Activo = activo;
        await _db.SaveChangesAsync();
        TempData["Exito"] = activo
            ? "Trabajador reactivado correctamente."
            : "Trabajador desactivado. Su historial se conserva.";
        return RedirectToAction(nameof(Index));
    }

    private async Task CargarAreas()
    {
        var areas = await _db.Areas.AsNoTracking()
            .OrderBy(a => a.Nombre)
            .Select(a => new SelectListItem(a.Nombre, a.IdArea.ToString()))
            .ToListAsync();
        ViewBag.Areas = areas;
    }
}
