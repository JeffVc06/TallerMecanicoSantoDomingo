using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TallerMecanicoSantoDomingo.Data;
using TallerMecanicoSantoDomingo.Services;

namespace TallerMecanicoSantoDomingo.Controllers;

/// <summary>
/// Reportes con consultas reales a SQL Server: servicios, trabajadores,
/// productos y facturación, con filtros por período y exportación a CSV.
/// </summary>
[Authorize]
public class ReportesController : Controller
{
    private readonly IReporteService _reportes;
    private readonly TallerDbContext _db;

    public ReportesController(IReporteService reportes, TallerDbContext db)
    {
        _reportes = reportes;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        await CargarListas();
        var desde = DateOnly.FromDateTime(DateTime.Today.AddMonths(-1));
        var hasta = DateOnly.FromDateTime(DateTime.Today);

        ViewBag.Servicios = await _reportes.ServiciosPorPeriodo(desde, hasta, null, null, null, null);
        ViewBag.Facturas = await _reportes.FacturasPorPeriodo(desde, hasta);
        ViewBag.Montos = await _reportes.TotalFacturadoPorPeriodo(desde, hasta);
        ViewBag.Carga = await _reportes.CargaActual();
        ViewBag.Productos = await _reportes.ProductosMasUtilizados(desde, hasta);
        ViewBag.Desde = desde;
        ViewBag.Hasta = hasta;
        return View();
    }

    /// <summary>Reporte de servicios por período con filtros combinados.</summary>
    [HttpGet]
    public async Task<IActionResult> Servicios(DateOnly desde, DateOnly hasta, string? tipo, string? estado, int? idCliente, int? idTrabajador)
    {
        var filas = await _reportes.ServiciosPorPeriodo(desde, hasta, tipo, estado, idCliente, idTrabajador);
        await CargarListas();
        ViewBag.PorTipo = await _reportes.ServiciosPorTipo(desde, hasta);
        ViewBag.PorEstado = await _reportes.ServiciosPorEstado(desde, hasta);
        ViewBag.Desde = desde;
        ViewBag.Hasta = hasta;
        ViewBag.Tipo = tipo;
        ViewBag.Estado = estado;
        ViewBag.IdCliente = idCliente;
        ViewBag.IdTrabajador = idTrabajador;
        return View(filas);
    }

    [HttpGet]
    public async Task<IActionResult> Vehiculos(DateOnly desde, DateOnly hasta)
    {
        var filas = await _reportes.ServiciosPorVehiculo(desde, hasta);
        ViewBag.Desde = desde;
        ViewBag.Hasta = hasta;
        return View(filas);
    }

    /// <summary>Historial completo de un cliente: sus vehículos y todos los servicios.</summary>
    [HttpGet]
    public async Task<IActionResult> Cliente(int? idCliente)
    {
        await CargarListas();
        if (idCliente.HasValue)
        {
            var cliente = await _db.Clientes.AsNoTracking().FirstOrDefaultAsync(c => c.IdCliente == idCliente.Value);
            ViewBag.Cliente = cliente;
            ViewBag.Vehiculos = await _db.Vehiculos.AsNoTracking()
                .Where(v => v.IdCliente == idCliente.Value).OrderBy(v => v.Placa).ToListAsync();
            ViewBag.Servicios = await _reportes.HistorialPorCliente(idCliente.Value);
        }
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Trabajadores()
    {
        ViewBag.PorTrabajador = await _reportes.TrabajosPorTrabajador();
        ViewBag.CargaActual = await _reportes.CargaActual();
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Facturas(DateOnly desde, DateOnly hasta)
    {
        ViewBag.Facturas = await _reportes.FacturasPorPeriodo(desde, hasta);
        ViewBag.Montos = await _reportes.TotalFacturadoPorPeriodo(desde, hasta);
        ViewBag.Desde = desde;
        ViewBag.Hasta = hasta;
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Productos(DateOnly? desde, DateOnly? hasta)
    {
        ViewBag.Productos = await _reportes.ProductosMasUtilizados(desde, hasta);
        ViewBag.Desde = desde;
        ViewBag.Hasta = hasta;
        return View();
    }

    // Exportación a CSV
    [HttpGet]
    public async Task<IActionResult> ExportarCsv(string tipo, DateOnly desde, DateOnly hasta, int? idCliente)
    {
        string contenido;
        string nombreArchivo;

        switch (tipo)
        {
            case "servicios":
            {
                var filas = await _reportes.ServiciosPorPeriodo(desde, hasta, null, null, idCliente, null);
                contenido = Csv(filas.Select(f => new[] { f.IdServicio.ToString(), f.Placa, f.Cliente, f.TipoServicio, f.Estado, f.FechaInicio.ToString("dd/MM/yyyy"), f.FechaFin?.ToString("dd/MM/yyyy") ?? "", f.Total.ToString("0.00") }),
                    new[] { "Servicio", "Placa", "Cliente", "Tipo", "Estado", "Fecha inicio", "Fecha fin", "Total" });
                nombreArchivo = $"servicios_{desde:yyyyMMdd}_{hasta:yyyyMMdd}.csv";
                break;
            }
            case "facturas":
            {
                var filas = await _reportes.FacturasPorPeriodo(desde, hasta);
                contenido = Csv(filas.Select(f => new[] { f.NumeroFactura, f.Fecha.ToString("dd/MM/yyyy"), f.Cliente, f.Subtotal.ToString("0.00"), f.IVA.ToString("0.00"), f.Total.ToString("0.00"), f.Estado }),
                    new[] { "Número", "Fecha", "Cliente", "Subtotal", "IVA", "Total", "Estado" });
                nombreArchivo = $"facturas_{desde:yyyyMMdd}_{hasta:yyyyMMdd}.csv";
                break;
            }
            case "productos":
            {
                var filas = await _reportes.ProductosMasUtilizados(desde, hasta);
                contenido = Csv(filas.Select(f => new[] { f.Producto, f.CantidadTotal.ToString("0.###"), f.VecesUtilizado.ToString(), f.MontoEstimado?.ToString("0.00") ?? "" }),
                    new[] { "Producto", "Cantidad total", "Veces utilizado", "Monto estimado" });
                nombreArchivo = $"productos_{desde:yyyyMMdd}_{hasta:yyyyMMdd}.csv";
                break;
            }
            default:
                TempData["Error"] = "Tipo de reporte no reconocido.";
                return RedirectToAction(nameof(Index));
        }

        return File(Encoding.UTF8.GetBytes("﻿" + contenido), "text/csv; encoding=utf-8", nombreArchivo);
    }

    private static string Csv(IEnumerable<string[]> filas, string[] encabezados)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(";", encabezados));
        foreach (var fila in filas)
            sb.AppendLine(string.Join(";", fila.Select(CampoCsv)));
        return sb.ToString();
    }

    private static string CampoCsv(string valor)
    {
        // Excel y otros programas pueden ejecutar valores controlados por usuarios
        // como fórmulas al abrir un CSV. Anteponer apóstrofo hace que se interpreten
        // como texto cuando el primer carácter significativo es un operador.
        var significativo = valor.TrimStart();
        if (significativo.Length > 0 && significativo[0] is '=' or '+' or '-' or '@')
            valor = "'" + valor;

        return valor.Contains(';') || valor.Contains('"') || valor.Contains('\n') || valor.Contains('\r')
            ? "\"" + valor.Replace("\"", "\"\"") + "\""
            : valor;
    }

    private async Task CargarListas()
    {
        ViewBag.Clientes = await _db.Clientes.AsNoTracking().OrderBy(c => c.Apellido)
            .Select(c => new SelectListItem($"{c.Nombre} {c.Apellido}", c.IdCliente.ToString())).ToListAsync();
        ViewBag.Trabajadores = await _db.Trabajadores.AsNoTracking().OrderBy(t => t.Apellido)
            .Select(t => new SelectListItem($"{t.Nombre} {t.Apellido}", t.IdTrabajador.ToString())).ToListAsync();
    }
}
