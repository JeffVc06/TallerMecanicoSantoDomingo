using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using TallerMecanicoSantoDomingo.Data;
using TallerMecanicoSantoDomingo.Models;
using TallerMecanicoSantoDomingo.Services;

namespace TallerMecanicoSantoDomingo.Tests;

/// <summary>
/// Utilidades compartidas por las pruebas: contexto en memoria con datos base
/// y configuración del taller.
/// </summary>
public static class FabricaDeContextos
{
    /// <summary>
    /// Crea un TallerDbContext sobre el proveedor en memoria de EF Core.
    /// Se ignoran las advertencias de transacción porque el proveedor en memoria
    /// no soporta transacciones reales; las transacciones y bloqueos en SQL Server
    /// se comprueban en las pruebas de integración.
    /// </summary>
    public static TallerDbContext EnMemoria(string? nombre = null)
    {
        var opciones = new DbContextOptionsBuilder<TallerDbContext>()
            .UseInMemoryDatabase(nombre ?? Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .EnableSensitiveDataLogging()
            .Options;

        return new TallerDbContext(opciones);
    }

    public static IOptions<ConfiguracionTaller> Configuracion(decimal porcentajeIva = 15m) =>
        Options.Create(new ConfiguracionTaller
        {
            NombreTaller = "Taller Mecánico Santo Domingo",
            PorcentajeIVA = porcentajeIva
        });

    /// <summary>
    /// Datos base: dos áreas, dos clientes, tres vehículos y dos trabajadores.
    /// Cliente 1: Juan Pérez (vehículos 1 y 2). Cliente 2: María Sánchez (vehículo 3).
    /// Trabajador 1: área Mantenimiento mecánico. Trabajador 2: área Pintura.
    /// </summary>
    public static void SembrarBase(TallerDbContext db)
    {
        db.Areas.AddRange(
            new AreaTrabajo { IdArea = 1, Nombre = "Mantenimiento mecánico" },
            new AreaTrabajo { IdArea = 2, Nombre = "Pintura" });

        db.Clientes.AddRange(
            new Cliente
            {
                IdCliente = 1, Nombre = "Juan", Apellido = "Pérez López",
                Telefono = "8888-1111", Direccion = "Managua", RFC = "RFC-0001", Activo = true
            },
            new Cliente
            {
                IdCliente = 2, Nombre = "María", Apellido = "Sánchez Ruiz",
                Telefono = "8777-2222", Direccion = "Managua", RFC = "RFC-0002", Activo = true
            });

        db.Vehiculos.AddRange(
            new Vehiculo { IdVehiculo = 1, IdCliente = 1, Placa = "MGA-1234", Marca = "Toyota", Modelo = "Corolla", Anio = 2018, Activo = true },
            new Vehiculo { IdVehiculo = 2, IdCliente = 1, Placa = "MGB-5678", Marca = "Nissan", Modelo = "Sentra", Anio = 2020, Activo = true },
            new Vehiculo { IdVehiculo = 3, IdCliente = 2, Placa = "MGC-9012", Marca = "Hyundai", Modelo = "Tucson", Anio = 2019, Activo = true });

        db.Trabajadores.AddRange(
            new Trabajador { IdTrabajador = 1, Nombre = "Pedro", Apellido = "Martínez", Telefono = "8555-4444", IdArea = 1, Activo = true },
            new Trabajador { IdTrabajador = 2, Nombre = "Ana", Apellido = "González", Telefono = "8333-6666", IdArea = 2, Activo = true });

        db.Productos.AddRange(
            new Producto { IdProducto = 1, Nombre = "Filtro de aceite", Descripcion = "Filtro", PrecioUnitario = 350m, Stock = 20m, Activo = true },
            new Producto { IdProducto = 2, Nombre = "Aceite 5W-30", Descripcion = "Aceite sintético", PrecioUnitario = 480m, Stock = 40m, Activo = true });

        db.SaveChanges();
    }

    /// <summary>Crea un servicio abierto directamente en la base en memoria (sin pasar por el servicio de negocio).</summary>
    public static Servicio NuevoServicio(TallerDbContext db, int idVehiculo, string tipo, decimal total,
        string descripcion = "Trabajo de prueba", string estado = "Abierto")
    {
        var servicio = new Servicio
        {
            IdVehiculo = idVehiculo,
            TipoServicio = tipo,
            Descripcion = descripcion,
            Estado = estado,
            FechaInicio = DateOnly.FromDateTime(new DateTime(2026, 9, 1)),
            Total = total
        };
        db.Servicios.Add(servicio);
        db.SaveChanges();
        return servicio;
    }
}
