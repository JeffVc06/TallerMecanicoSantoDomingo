using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TallerMecanicoSantoDomingo.Services;

namespace TallerMecanicoSantoDomingo.Controllers;

/// <summary>Panel principal con indicadores y gráficos consultados desde SQL Server.</summary>
[Authorize]
public class DashboardController : Controller
{
    private readonly IDashboardService _dashboard;

    public DashboardController(IDashboardService dashboard) => _dashboard = dashboard;

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var datos = await _dashboard.ObtenerDatos();
        return View(datos);
    }
}
