using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TallerMecanicoSantoDomingo.Models;

namespace TallerMecanicoSantoDomingo.Data;

/// <summary>Las nueve tablas del diagrama y las cuentas de acceso en Usuarios.</summary>
public class TallerDbContext(DbContextOptions<TallerDbContext> options) : DbContext(options)
{
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Vehiculo> Vehiculos => Set<Vehiculo>();
    public DbSet<Trabajador> Trabajadores => Set<Trabajador>();
    public DbSet<Servicio> Servicios => Set<Servicio>();
    public DbSet<DetalleServicio> DetalleServicio => Set<DetalleServicio>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<ProductoUtilizado> ProductosUtilizados => Set<ProductoUtilizado>();
    public DbSet<Factura> Facturas => Set<Factura>();
    public DbSet<DetalleFactura> DetalleFactura => Set<DetalleFactura>();
    public DbSet<IdentityUser> Users => Set<IdentityUser>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        // Los nombres, tamaños y decimales están declarados en los modelos.
        // Aquí solo quedan las relaciones y las restricciones de SQL Server.
        model.HasDefaultSchema("dbo");
        model.Entity<Cliente>(e =>
        {
            e.HasIndex(c => c.RFC).IsUnique();
            e.HasIndex(c => c.Cedula).IsUnique().HasFilter("Cedula IS NOT NULL");
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Cliente_Nombre", "LTRIM(Nombre) <> ''");
                t.HasCheckConstraint("CK_Cliente_Apellido", "LTRIM(Apellido) <> ''");
            });
        });
        model.Entity<Vehiculo>(e =>
        {
            e.HasOne(v => v.Cliente).WithMany(c => c.Vehiculos).HasForeignKey(v => v.IdCliente);
            e.HasIndex(v => v.Placa).IsUnique();
            e.HasIndex(v => v.NumChasis).IsUnique().HasFilter("NumChasis IS NOT NULL");
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Vehiculo_Anio", "Anio BETWEEN 1950 AND YEAR(GETDATE()) + 1");
                t.HasCheckConstraint("CK_Vehiculo_Kilometraje", "Kilometraje >= 0");
            });
        });
        model.Entity<Trabajador>().ToTable(t =>
        {
            t.HasCheckConstraint("CK_Trabajador_Area", "Area IN (N'Mantenimiento mecánico', N'Pintura')");
            t.HasCheckConstraint("CK_Trabajador_Fechas", "FechaSalida IS NULL OR FechaSalida >= CAST(FechaIngreso AS date)");
        });
        model.Entity<Servicio>(e =>
        {
            e.HasOne(s => s.Vehiculo).WithMany(v => v.Servicios).HasForeignKey(s => s.IdVehiculo);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Servicio_Tipo", "Tipo IN ('Mantenimiento', 'Pintura')");
                t.HasCheckConstraint("CK_Servicio_Estado", "Estado IN ('Abierto', 'Finalizado', 'Cancelado')");
                t.HasCheckConstraint("CK_Servicio_FechaFin", "FechaFin IS NULL OR FechaFin >= FechaInicio");
                t.HasCheckConstraint("CK_Servicio_Costo", "Costo >= 0");
            });
        });
        model.Entity<DetalleServicio>(e =>
        {
            e.HasOne(d => d.Servicio).WithMany(s => s.DetalleServicios).HasForeignKey(d => d.IdServicio);
            e.HasOne(d => d.Trabajador).WithMany(t => t.DetalleServicios).HasForeignKey(d => d.IdTrabajador);
            e.ToTable(t => t.HasCheckConstraint("CK_DetalleServicio_Estado", "Estado IN ('Activo', 'Retirado')"));
        });
        model.Entity<Producto>().ToTable(t =>
        {
            t.HasCheckConstraint("CK_Producto_Precio", "Precio >= 0");
            t.HasCheckConstraint("CK_Producto_Costo", "Costo >= 0");
            t.HasCheckConstraint("CK_Producto_Stock", "Stock >= 0");
        });
        model.Entity<ProductoUtilizado>(e =>
        {
            e.HasOne(p => p.Servicio).WithMany(s => s.ProductosUtilizados).HasForeignKey(p => p.IdServicio);
            e.HasOne(p => p.Producto).WithMany(p => p.ProductosUtilizados).HasForeignKey(p => p.IdProducto);
            e.ToTable(t => t.HasCheckConstraint("CK_ProductosUtilizados_Cantidad", "Cantidad > 0"));
        });
        model.Entity<Factura>(e =>
        {
            e.HasOne(f => f.Cliente).WithMany(c => c.Facturas).HasForeignKey(f => f.IdCliente);
            e.HasIndex(f => f.NumeroFactura).IsUnique();
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Factura_MetodoPago", "MetodoPago IN ('Efectivo', 'Tarjeta', 'Transferencia')");
                t.HasCheckConstraint("CK_Factura_Estado", "Estado IN ('Emitida', 'Anulada')");
                t.HasCheckConstraint("CK_Factura_Importes", "Subtotal >= 0 AND IVA >= 0 AND Total = Subtotal + IVA");
            });
        });
        model.Entity<DetalleFactura>(e =>
        {
            e.HasOne(d => d.Factura).WithMany(f => f.DetalleFacturas).HasForeignKey(d => d.IdFactura);
            e.HasOne(d => d.Servicio).WithMany(s => s.DetalleFacturas).HasForeignKey(d => d.IdServicio);
            e.ToTable(t => t.HasCheckConstraint("CK_DetalleFactura_Subtotal", "Subtotal >= 0"));
        });
        model.Entity<IdentityUser>(e =>
        {
            e.ToTable("Usuarios");
            e.Property(u => u.Id).HasMaxLength(450);
            e.Property(u => u.UserName).HasMaxLength(256);
            e.Property(u => u.NormalizedUserName).HasMaxLength(256);
            e.Property(u => u.Email).HasMaxLength(256);
            e.Property(u => u.NormalizedEmail).HasMaxLength(256);
            e.Property(u => u.ConcurrencyStamp).IsConcurrencyToken();
            e.HasIndex(u => u.NormalizedUserName).IsUnique().HasFilter("NormalizedUserName IS NOT NULL");
            e.HasIndex(u => u.NormalizedEmail).IsUnique().HasFilter("NormalizedEmail IS NOT NULL");
            // La aplicación solo permite acceso con correo y contraseña.
            e.Ignore(u => u.PhoneNumber);
            e.Ignore(u => u.PhoneNumberConfirmed);
            e.Ignore(u => u.TwoFactorEnabled);
        });
        foreach (var foreignKey in model.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
            foreignKey.DeleteBehavior = DeleteBehavior.Restrict;
    }
}
