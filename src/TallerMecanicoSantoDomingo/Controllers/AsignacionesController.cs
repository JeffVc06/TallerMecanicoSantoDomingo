using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TallerMecanicoSantoDomingo.Data;
using TallerMecanicoSantoDomingo.Models;
using TallerMecanicoSantoDomingo.Services;
using TallerMecanicoSantoDomingo.ViewModels;

namespace TallerMecanicoSantoDomingo.Controllers;

/// <summary>
/// Módulo de asignación de personal (MER: ASIGNADO_A; Reglas 5, 6 y 7).
/// Incluye respuestas JSON para actualizar la información sin recargar la página.
/// </summary>
[Authorize]
public class AsignacionesController : Controller
{
    private readonly IAsignacionService _asignaciones;
    private readonly TallerDbContext _db;

    public AsignacionesController(IAsignacionService asignaciones, TallerDbContext db)
    {
        _asignaciones = asignaciones;
        _db = db;
    }

    /// <summary>Página principal: selección del servicio y trabajadores compatibles.</summary>
    [HttpGet]
    public async Task<IActionResult> Index(int? idServicio)
    {
        var serviciosAbiertos = await _db.Servicios.AsNoTracking()
            .Include(s => s.Vehiculo).ThenInclude(v => v!.Cliente)
            .Where(s => s.Estado == "Abierto")
            .OrderByDescending(s => s.FechaRegistro)
            .Select(s => new
            {
                s.IdServicio,
                Etiqueta = $"#{s.IdServicio} — {s.Vehiculo!.Placa} — {s.TipoServicio} — {s.Descripcion}"
            })
            .ToListAsync();

        ViewBag.ServiciosAbiertos = serviciosAbiertos
            .Select(s => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem(s.Etiqueta, s.IdServicio.ToString()))
            .ToList();

        if (idServicio.HasValue)
        {
            var servicio = await _db.Servicios.AsNoTracking()
                .Include(s => s.Vehiculo).ThenInclude(v => v!.Cliente)
                .FirstOrDefaultAsync(s => s.IdServicio == idServicio.Value);
            ViewBag.Servicio = servicio;
            ViewBag.Compatibles = await _asignaciones.TrabajadoresCompatibles(idServicio.Value, soloDisponibles: false);
            ViewBag.Asignados = await _asignaciones.AsignacionesDelServicio(idServicio.Value);
        }

        return View();
    }

    /// <summary>Reglas 5, 6 y 7 se validan en el servidor dentro del servicio de negocio.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Asignar(AsignarTrabajadorViewModel modelo)
    {
        var resultado = await _asignaciones.Asignar(modelo.IdServicio, modelo.IdTrabajador, modelo.Observaciones);
        TempData[resultado.EsCorrecto ? "Exito" : "Error"] = resultado.Mensaje;
        return RedirectToAction(nameof(Index), new { idServicio = modelo.IdServicio });
    }

    /// <summary>Versión AJAX de la asignación: responde JSON para actualizar la lista al instante.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AsignarAjax([FromForm] int idServicio, [FromForm] int idTrabajador, [FromForm] string? observaciones)
    {
        var resultado = await _asignaciones.Asignar(idServicio, idTrabajador, observaciones);

        var compatibles = await _asignaciones.TrabajadoresCompatibles(idServicio, soloDisponibles: false);
        var asignados = await _asignaciones.AsignacionesDelServicio(idServicio);

        return Json(new
        {
            esCorrecto = resultado.EsCorrecto,
            mensaje = resultado.Mensaje,
            compatibles = compatibles.Select(c => new
            {
                c.IdTrabajador, c.NombreCompleto, c.Area, c.ServiciosActivos, c.Disponible
            }),
            asignados = asignados.Select(a => new
            {
                a.IdDetalleServicio, a.NombreTrabajador, a.Area, a.EstadoAsignacion,
                a.FechaAsignacion, a.Observaciones
            })
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Desasignar(int idDetalleServicio, int idServicio)
    {
        var resultado = await _asignaciones.Desasignar(idDetalleServicio);
        TempData[resultado.EsCorrecto ? "Exito" : "Error"] = resultado.Mensaje;
        return RedirectToAction(nameof(Index), new { idServicio });
    }
}
