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
/// Gestión de servicios (Reglas 3, 4, 8 y 10).
/// La pantalla de detalles concentra: trabajadores asignados, productos
/// utilizados, cambio de estado y finalización.
/// </summary>
[Authorize]
public class ServiciosController : Controller
{
    private readonly ServicioService _servicios;
    private readonly AsignacionService _asignaciones;
    private readonly ProductoService _productos;
    private readonly TallerDbContext _db;

    public ServiciosController(
        ServicioService servicios,
        AsignacionService asignaciones,
        ProductoService productos,
        TallerDbContext db)
    {
        _servicios = servicios;
        _asignaciones = asignaciones;
        _productos = productos;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? buscar, string? tipo, string? estado, int pagina = 1)
    {
        var resultado = await _servicios.Buscar(buscar, tipo, estado, null, pagina, 10);
        ViewBag.Buscar = buscar;
        ViewBag.Tipo = tipo;
        ViewBag.Estado = estado;
        ViewBag.Tipos = (new[] { "Mantenimiento", "Pintura" });
        ViewBag.Estados = (new[] { "Abierto", "Finalizado", "Cancelado" });
        return View(resultado);
    }

    [HttpGet]
    public async Task<IActionResult> Crear(int? idVehiculo = null)
    {
        await CargarVehiculos();
        return View(new ServicioFormViewModel { IdVehiculo = idVehiculo ?? 0 });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(ServicioFormViewModel modelo)
    {
        if (!ModelState.IsValid)
        {
            await CargarVehiculos();
            return View(modelo);
        }

        var servicio = new Servicio
        {
            IdVehiculo = modelo.IdVehiculo,
            TipoServicio = modelo.TipoServicio,
            Descripcion = modelo.Descripcion,
            DescripcionFalla = modelo.TipoServicio == "Mantenimiento" ? modelo.DescripcionFalla : null,
            TipoPintura = modelo.TipoServicio == "Pintura" ? modelo.TipoPintura : null,
            Color = modelo.TipoServicio == "Pintura" ? modelo.Color : null,
            FechaInicio = modelo.FechaInicio,
            Total = modelo.Total
        };

        var resultado = await _servicios.Crear(servicio);
        if (!resultado.EsCorrecto)
        {
            ModelState.AddModelError(string.Empty, resultado.Mensaje ?? "No se pudo registrar el servicio.");
            await CargarVehiculos();
            return View(modelo);
        }

        TempData["Exito"] = resultado.Mensaje + " Ahora puede asignar trabajadores y registrar productos utilizados.";
        return RedirectToAction(nameof(Detalles), new { id = resultado.Datos!.IdServicio });
    }

    [HttpGet]
    public async Task<IActionResult> Detalles(int id)
    {
        var servicio = await _servicios.ObtenerPorId(id);
        if (servicio is null)
            return RedirectToAction(nameof(Index));

        ViewBag.Asignaciones = await _asignaciones.AsignacionesDelServicio(id);
        ViewBag.TrabajadoresCompatibles = await _asignaciones.TrabajadoresCompatibles(id, soloDisponibles: false);
        ViewBag.Cargas = await _servicios.CargasDeTrabajadores(
            servicio.DetalleServicios.Select(d => d.IdTrabajador).ToList());
        ViewBag.ProductosActivos = await _productos.ListarActivosParaSeleccion();
        return View(servicio);
    }

    [HttpGet]
    public async Task<IActionResult> Editar(int id)
    {
        var servicio = await _servicios.ObtenerPorId(id);
        if (servicio is null)
            return RedirectToAction(nameof(Index));

        await CargarVehiculos();
        return View(new ServicioFormViewModel
        {
            IdServicio = servicio.IdServicio,
            IdVehiculo = servicio.IdVehiculo,
            TipoServicio = servicio.TipoServicio,
            Descripcion = servicio.Descripcion,
            DescripcionFalla = servicio.DescripcionFalla,
            TipoPintura = servicio.TipoPintura,
            Color = servicio.Color,
            FechaInicio = servicio.FechaInicio,
            Total = servicio.Total
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(ServicioFormViewModel modelo)
    {
        if (!ModelState.IsValid)
        {
            await CargarVehiculos();
            return View(modelo);
        }

        var resultado = await _servicios.Editar(new Servicio
        {
            IdServicio = modelo.IdServicio,
            IdVehiculo = modelo.IdVehiculo,
            TipoServicio = modelo.TipoServicio,
            Descripcion = modelo.Descripcion,
            DescripcionFalla = modelo.TipoServicio == "Mantenimiento" ? modelo.DescripcionFalla : null,
            TipoPintura = modelo.TipoServicio == "Pintura" ? modelo.TipoPintura : null,
            Color = modelo.TipoServicio == "Pintura" ? modelo.Color : null,
            FechaInicio = modelo.FechaInicio,
            Total = modelo.Total
        });

        if (!resultado.EsCorrecto)
        {
            ModelState.AddModelError(string.Empty, resultado.Mensaje ?? "No se pudo actualizar el servicio.");
            await CargarVehiculos();
            return View(modelo);
        }

        TempData["Exito"] = resultado.Mensaje;
        return RedirectToAction(nameof(Detalles), new { id = modelo.IdServicio });
    }

    /// <summary>Regla 8: finalización con fecha obligatoria y no anterior al inicio.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Finalizar(FinalizarServicioViewModel modelo)
    {
        var resultado = await _servicios.Finalizar(modelo.IdServicio, modelo.FechaFin);
        TempData[resultado.EsCorrecto ? "Exito" : "Error"] = resultado.Mensaje;
        return RedirectToAction(nameof(Detalles), new { id = modelo.IdServicio });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancelar(CancelarServicioViewModel modelo)
    {
        var resultado = await _servicios.Cancelar(modelo.IdServicio, modelo.Motivo);
        TempData[resultado.EsCorrecto ? "Exito" : "Error"] = resultado.Mensaje;
        return RedirectToAction(nameof(Detalles), new { id = modelo.IdServicio });
    }

    // Productos utilizados por servicio (Reglas 9 y 10)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AgregarProducto(ProductoUtilizadoFormViewModel modelo)
    {
        var resultado = await _productos.AgregarAlServicio(modelo.IdServicio, modelo.IdProducto, modelo.Cantidad);
        TempData[resultado.EsCorrecto ? "Exito" : "Error"] = resultado.Mensaje;
        return RedirectToAction(nameof(Detalles), new { id = modelo.IdServicio });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ActualizarProducto(int idProducto, int idServicio, decimal cantidad)
    {
        var resultado = await _productos.ActualizarCantidad(idServicio, idProducto, cantidad);
        TempData[resultado.EsCorrecto ? "Exito" : "Error"] = resultado.Mensaje;
        return RedirectToAction(nameof(Detalles), new { id = idServicio });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuitarProducto(int idProducto, int idServicio)
    {
        var resultado = await _productos.QuitarDelServicio(idServicio, idProducto);
        TempData[resultado.EsCorrecto ? "Exito" : "Error"] = resultado.Mensaje;
        return RedirectToAction(nameof(Detalles), new { id = idServicio });
    }

    private async Task CargarVehiculos()
    {
        var vehiculos = await _db.Vehiculos.AsNoTracking()
            .Where(v => v.Activo)
            .Include(v => v.Cliente)
            .OrderBy(v => v.Placa)
            .Select(v => new SelectListItem(
                $"{v.Placa} — {v.Marca} {v.Modelo} ({v.Cliente!.Nombre} {v.Cliente.Apellido})",
                v.IdVehiculo.ToString()))
            .ToListAsync();
        ViewBag.Vehiculos = vehiculos;
    }
}
