using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TallerMecanicoSantoDomingo.Data;
using TallerMecanicoSantoDomingo.Services;

namespace TallerMecanicoSantoDomingo.Controllers;

/// <summary>Historial del vehículo: todos los servicios recibidos con sus relaciones.</summary>
[Authorize]
public class HistorialController : Controller
{
    private readonly IVehiculoService _vehiculos;
    private readonly TallerDbContext _db;

    public HistorialController(IVehiculoService vehiculos, TallerDbContext db)
    {
        _vehiculos = vehiculos;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? idVehiculo)
    {
        var vehiculos = await _db.Vehiculos.AsNoTracking()
            .Include(v => v.Cliente)
            .OrderBy(v => v.Placa)
            .Select(v => new SelectListItem(
                $"{v.Placa} — {v.Marca} {v.Modelo} ({v.Cliente!.Nombre} {v.Cliente.Apellido})",
                v.IdVehiculo.ToString()))
            .ToListAsync();
        ViewBag.Vehiculos = vehiculos;

        if (idVehiculo.HasValue)
        {
            var vehiculo = await _vehiculos.ObtenerPorId(idVehiculo.Value);
            if (vehiculo is not null)
            {
                ViewBag.Vehiculo = vehiculo;
                ViewBag.Historial = await _vehiculos.ObtenerHistorial(idVehiculo.Value);
            }
        }

        return View();
    }
}
