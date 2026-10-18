using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TallerMecanicoSantoDomingo.Models;

namespace TallerMecanicoSantoDomingo.Data;

/// <summary>Contexto de datos del taller y de las cuentas de acceso.</summary>
public class TallerDbContext : IdentityDbContext
{
    public TallerDbContext(DbContextOptions<TallerDbContext> options) : base(options)
    {
    }

    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Vehiculo> Vehiculos => Set<Vehiculo>();
    public DbSet<AreaTrabajo> Areas => Set<AreaTrabajo>();
    public DbSet<Trabajador> Trabajadores => Set<Trabajador>();
    public DbSet<Servicio> Servicios => Set<Servicio>();
    public DbSet<DetalleServicio> DetalleServicio => Set<DetalleServicio>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<ProductoUtilizado> ProductosUtilizados => Set<ProductoUtilizado>();
    public DbSet<Factura> Facturas => Set<Factura>();
    public DbSet<DetalleFactura> DetalleFactura => Set<DetalleFactura>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder); // Tablas de ASP.NET Core Identity (AspNet*)

        // CLIENTES
        builder.Entity<Cliente>(e =>
        {
            e.ToTable("Clientes");
            e.HasKey(c => c.IdCliente);
            e.Property(c => c.IdCliente).HasColumnName("IdCliente").ValueGeneratedOnAdd();
            e.Property(c => c.Nombre).HasColumnName("Nombre").HasMaxLength(100).IsRequired();
            e.Property(c => c.Apellido).HasColumnName("Apellido").HasMaxLength(100).IsRequired();
            e.Property(c => c.Telefono).HasColumnName("Telefono").HasMaxLength(20).IsRequired();
            e.Property(c => c.Direccion).HasColumnName("Direccion").HasMaxLength(200).IsRequired();
            e.Property(c => c.RFC).HasColumnName("RFC").HasMaxLength(20).IsRequired();
            e.Property(c => c.Activo).HasColumnName("Activo").IsRequired();
            e.Property(c => c.FechaRegistro).HasColumnName("FechaRegistro").IsRequired();
            // Regla 1: identificador único e identificación fiscal única.
            e.HasIndex(c => c.RFC).IsUnique().HasDatabaseName("UQ_Clientes_RFC");
            e.HasIndex(c => new { c.Nombre, c.Apellido }).HasDatabaseName("IX_Clientes_Nombre_Apellido");
            e.HasIndex(c => c.Telefono).HasDatabaseName("IX_Clientes_Telefono");
            e.HasCheckConstraint("CK_Clientes_Nombre_NoVacio", "LTRIM(Nombre) <> ''");
            e.HasCheckConstraint("CK_Clientes_Apellido_NoVacio", "LTRIM(Apellido) <> ''");
        });

        // VEHICULOS
        builder.Entity<Vehiculo>(e =>
        {
            e.ToTable("Vehiculos");
            e.HasKey(v => v.IdVehiculo);
            e.Property(v => v.IdVehiculo).HasColumnName("IdVehiculo").ValueGeneratedOnAdd();
            e.Property(v => v.IdCliente).HasColumnName("IdCliente").IsRequired();
            e.Property(v => v.Placa).HasColumnName("Placa").HasMaxLength(20).IsRequired();
            e.Property(v => v.Marca).HasColumnName("Marca").HasMaxLength(50).IsRequired();
            e.Property(v => v.Modelo).HasColumnName("Modelo").HasMaxLength(50).IsRequired();
            e.Property(v => v.Anio).HasColumnName("Anio").IsRequired();
            e.Property(v => v.Activo).HasColumnName("Activo").IsRequired();
            e.Property(v => v.FechaRegistro).HasColumnName("FechaRegistro").IsRequired();
            // Regla 2: cada vehículo pertenece a un único cliente (NO ACTION: conservar historial).
            e.HasOne(v => v.Cliente).WithMany(c => c.Vehiculos)
                .HasForeignKey(v => v.IdCliente).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_Vehiculos_Clientes");
            // Regla 1: la placa identifica al vehículo de forma única.
            e.HasIndex(v => v.Placa).IsUnique().HasDatabaseName("UQ_Vehiculos_Placa");
            e.HasIndex(v => v.IdCliente).HasDatabaseName("IX_Vehiculos_IdCliente");
            e.HasCheckConstraint("CK_Vehiculos_Anio", "Anio >= 1950 AND Anio <= YEAR(GETDATE()) + 1");
        });

        // AREAS
        builder.Entity<AreaTrabajo>(e =>
        {
            e.ToTable("Areas");
            e.HasKey(a => a.IdArea);
            e.Property(a => a.IdArea).HasColumnName("IdArea").ValueGeneratedOnAdd();
            e.Property(a => a.Nombre).HasColumnName("Nombre").HasMaxLength(50).IsRequired();
            e.HasIndex(a => a.Nombre).IsUnique().HasDatabaseName("UQ_Areas_Nombre");
        });

        // TRABAJADORES
        builder.Entity<Trabajador>(e =>
        {
            e.ToTable("Trabajadores");
            e.HasKey(t => t.IdTrabajador);
            e.Property(t => t.IdTrabajador).HasColumnName("IdTrabajador").ValueGeneratedOnAdd();
            e.Property(t => t.Nombre).HasColumnName("Nombre").HasMaxLength(100).IsRequired();
            e.Property(t => t.Apellido).HasColumnName("Apellido").HasMaxLength(100).IsRequired();
            e.Property(t => t.Telefono).HasColumnName("Telefono").HasMaxLength(20).IsRequired();
            e.Property(t => t.IdArea).HasColumnName("IdArea").IsRequired();
            e.Property(t => t.Activo).HasColumnName("Activo").IsRequired();
            e.Property(t => t.FechaRegistro).HasColumnName("FechaRegistro").IsRequired();
            // Regla 5: cada trabajador pertenece a una sola área (PERTENECE, N:1).
            e.HasOne(t => t.Area).WithMany(a => a.Trabajadores)
                .HasForeignKey(t => t.IdArea).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_Trabajadores_Areas");
            e.HasIndex(t => t.IdArea).HasDatabaseName("IX_Trabajadores_IdArea");
        });

        // SERVICIOS
        builder.Entity<Servicio>(e =>
        {
            e.ToTable("Servicios");
            e.HasKey(s => s.IdServicio);
            e.Property(s => s.IdServicio).HasColumnName("IdServicio").ValueGeneratedOnAdd();
            e.Property(s => s.IdVehiculo).HasColumnName("IdVehiculo").IsRequired();
            e.Property(s => s.TipoServicio).HasColumnName("TipoServicio").HasMaxLength(20).IsRequired();
            e.Property(s => s.Descripcion).HasColumnName("Descripcion").HasMaxLength(500).IsRequired();
            e.Property(s => s.DescripcionFalla).HasColumnName("DescripcionFalla").HasMaxLength(500);
            e.Property(s => s.TipoPintura).HasColumnName("TipoPintura").HasMaxLength(50);
            e.Property(s => s.Color).HasColumnName("Color").HasMaxLength(50);
            e.Property(s => s.Estado).HasColumnName("Estado").HasMaxLength(20).IsRequired();
            e.Property(s => s.FechaInicio).HasColumnName("FechaInicio").IsRequired();
            e.Property(s => s.FechaFin).HasColumnName("FechaFin");
            e.Property(s => s.Total).HasColumnName("Total").HasPrecision(12, 2).IsRequired();
            e.Property(s => s.FechaRegistro).HasColumnName("FechaRegistro").IsRequired();
            // Regla 3: cada servicio corresponde a un solo vehículo (TIENE, 1:N).
            e.HasOne(s => s.Vehiculo).WithMany(v => v.Servicios)
                .HasForeignKey(s => s.IdVehiculo).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_Servicios_Vehiculos");
            e.HasIndex(s => s.IdVehiculo).HasDatabaseName("IX_Servicios_IdVehiculo");
            e.HasIndex(s => s.Estado).HasDatabaseName("IX_Servicios_Estado");
            e.HasIndex(s => s.FechaInicio).HasDatabaseName("IX_Servicios_FechaInicio");
            e.HasIndex(s => s.TipoServicio).HasDatabaseName("IX_Servicios_TipoServicio");
            // Regla 4: el tipo de servicio es Mantenimiento o Pintura.
            e.HasCheckConstraint("CK_Servicios_TipoServicio",
                "TipoServicio IN ('Mantenimiento', 'Pintura')");
            // Reglas 4 y 8: estado Abierto, Finalizado o Cancelado.
            e.HasCheckConstraint("CK_Servicios_Estado",
                "Estado IN ('Abierto', 'Finalizado', 'Cancelado')");
            // Regla 8: la fecha de finalización no puede ser anterior a la fecha de inicio.
            e.HasCheckConstraint("CK_Servicios_FechaFin",
                "FechaFin IS NULL OR FechaFin >= FechaInicio");
            // Regla 10: el monto del servicio debe ser mayor o igual que cero.
            e.HasCheckConstraint("CK_Servicios_Total", "Total >= 0");
        });

        // DETALLE_SERVICIO (ASIGNADO_A)
        builder.Entity<DetalleServicio>(e =>
        {
            e.ToTable("DetalleServicio");
            e.HasKey(d => d.IdDetalleServicio);
            e.Property(d => d.IdDetalleServicio).HasColumnName("IdDetalleServicio").ValueGeneratedOnAdd();
            e.Property(d => d.IdServicio).HasColumnName("IdServicio").IsRequired();
            e.Property(d => d.IdTrabajador).HasColumnName("IdTrabajador").IsRequired();
            e.Property(d => d.FechaAsignacion).HasColumnName("FechaAsignacion").IsRequired();
            e.Property(d => d.Estado).HasColumnName("Estado").HasMaxLength(20).IsRequired();
            e.Property(d => d.Observaciones).HasColumnName("Observaciones").HasMaxLength(200);
            // Regla 6: la asignación es la relación entre servicios y trabajadores.
            e.HasOne(d => d.Servicio).WithMany(s => s.DetalleServicios)
                .HasForeignKey(d => d.IdServicio).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_DetalleServicio_Servicios");
            e.HasOne(d => d.Trabajador).WithMany(t => t.DetalleServicios)
                .HasForeignKey(d => d.IdTrabajador).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_DetalleServicio_Trabajadores");
            // Regla 6: la pareja servicio-trabajador no puede repetirse.
            e.HasIndex(d => new { d.IdServicio, d.IdTrabajador }).IsUnique()
                .HasDatabaseName("UQ_DetalleServicio_Servicio_Trabajador");
            e.HasIndex(d => d.IdTrabajador).HasDatabaseName("IX_DetalleServicio_IdTrabajador");
            e.HasCheckConstraint("CK_DetalleServicio_Estado",
                "Estado IN ('Activo', 'Retirado')");
        });

        // PRODUCTOS (REPUESTOS)
        builder.Entity<Producto>(e =>
        {
            e.ToTable("Productos");
            e.HasKey(p => p.IdProducto);
            e.Property(p => p.IdProducto).HasColumnName("IdProducto").ValueGeneratedOnAdd();
            e.Property(p => p.Nombre).HasColumnName("Nombre").HasMaxLength(100).IsRequired();
            e.Property(p => p.Descripcion).HasColumnName("Descripcion").HasMaxLength(300).IsRequired();
            e.Property(p => p.PrecioUnitario).HasColumnName("PrecioUnitario").HasPrecision(12, 2).IsRequired();
            e.Property(p => p.Stock).HasColumnName("Stock").HasPrecision(12, 3).IsRequired();
            e.Property(p => p.Activo).HasColumnName("Activo").IsRequired();
            e.Property(p => p.FechaRegistro).HasColumnName("FechaRegistro").IsRequired();
            // Regla 10: precios y existencias no negativos.
            e.HasCheckConstraint("CK_Productos_PrecioUnitario", "PrecioUnitario >= 0");
            e.HasCheckConstraint("CK_Productos_Stock", "Stock >= 0");
            e.HasIndex(p => p.Nombre).HasDatabaseName("IX_Productos_Nombre");
        });

        // PRODUCTOS_UTILIZADOS (UTILIZA)
        builder.Entity<ProductoUtilizado>(e =>
        {
            e.ToTable("ProductosUtilizados");
            e.HasKey(pu => pu.IdProductoUtilizado);
            e.Property(pu => pu.IdProductoUtilizado).HasColumnName("IdProductoUtilizado").ValueGeneratedOnAdd();
            e.Property(pu => pu.IdServicio).HasColumnName("IdServicio").IsRequired();
            e.Property(pu => pu.IdProducto).HasColumnName("IdProducto").IsRequired();
            e.Property(pu => pu.Cantidad).HasColumnName("Cantidad").HasPrecision(12, 3).IsRequired();
            // Regla 9: relación N:M entre servicios y productos.
            e.HasOne(pu => pu.Servicio).WithMany(s => s.ProductosUtilizados)
                .HasForeignKey(pu => pu.IdServicio).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_ProductosUtilizados_Servicios");
            e.HasOne(pu => pu.Producto).WithMany(p => p.ProductosUtilizados)
                .HasForeignKey(pu => pu.IdProducto).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_ProductosUtilizados_Productos");
            // Regla 9: la pareja producto-servicio no se repite.
            e.HasIndex(pu => new { pu.IdServicio, pu.IdProducto }).IsUnique()
                .HasDatabaseName("UQ_ProductosUtilizados_Servicio_Producto");
            e.HasIndex(pu => pu.IdProducto).HasDatabaseName("IX_ProductosUtilizados_IdProducto");
            // Regla 10: la cantidad utilizada debe ser mayor que cero.
            e.HasCheckConstraint("CK_ProductosUtilizados_Cantidad", "Cantidad > 0");
        });

        // FACTURAS
        builder.Entity<Factura>(e =>
        {
            e.ToTable("Facturas");
            e.HasKey(f => f.IdFactura);
            e.Property(f => f.IdFactura).HasColumnName("IdFactura").ValueGeneratedOnAdd();
            e.Property(f => f.NumeroFactura).HasColumnName("NumeroFactura").HasMaxLength(20).IsRequired();
            e.Property(f => f.IdCliente).HasColumnName("IdCliente").IsRequired();
            e.Property(f => f.FechaFactura).HasColumnName("FechaFactura").IsRequired();
            e.Property(f => f.Subtotal).HasColumnName("Subtotal").HasPrecision(12, 2).IsRequired();
            e.Property(f => f.IVA).HasColumnName("IVA").HasPrecision(12, 2).IsRequired();
            e.Property(f => f.Total).HasColumnName("Total").HasPrecision(12, 2).IsRequired();
            e.Property(f => f.MetodoPago).HasColumnName("MetodoPago").HasMaxLength(30).IsRequired();
            e.Property(f => f.Estado).HasColumnName("Estado").HasMaxLength(20).IsRequired();
            e.Property(f => f.Notas).HasColumnName("Notas").HasMaxLength(200);
            // Relación GENERA: la factura corresponde a un cliente (Regla 12).
            e.HasOne(f => f.Cliente).WithMany(c => c.Facturas)
                .HasForeignKey(f => f.IdCliente).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_Facturas_Clientes");
            e.HasIndex(f => f.NumeroFactura).IsUnique().HasDatabaseName("UQ_Facturas_NumeroFactura");
            e.HasIndex(f => f.IdCliente).HasDatabaseName("IX_Facturas_IdCliente");
            e.HasIndex(f => f.FechaFactura).HasDatabaseName("IX_Facturas_FechaFactura");
            e.HasCheckConstraint("CK_Facturas_MetodoPago",
                "MetodoPago IN ('Efectivo', 'Tarjeta', 'Transferencia')");
            e.HasCheckConstraint("CK_Facturas_Estado",
                "Estado IN ('Emitida', 'Anulada')");
            // Reglas 10 y 13: importes no negativos y Total = Subtotal + IVA.
            e.HasCheckConstraint("CK_Facturas_Importes",
                "Subtotal >= 0 AND IVA >= 0 AND Total >= 0 AND Total = Subtotal + IVA");
        });

        // DETALLE_FACTURA
        builder.Entity<DetalleFactura>(e =>
        {
            e.ToTable("DetalleFactura");
            e.HasKey(df => df.IdDetalleFactura);
            e.Property(df => df.IdDetalleFactura).HasColumnName("IdDetalleFactura").ValueGeneratedOnAdd();
            e.Property(df => df.IdFactura).HasColumnName("IdFactura").IsRequired();
            e.Property(df => df.IdServicio).HasColumnName("IdServicio").IsRequired();
            e.Property(df => df.Subtotal).HasColumnName("Subtotal").HasPrecision(12, 2).IsRequired();
            // Regla 11: cada detalle referencia una factura y un servicio existentes.
            e.HasOne(df => df.Factura).WithMany(f => f.DetalleFacturas)
                .HasForeignKey(df => df.IdFactura).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_DetalleFactura_Facturas");
            e.HasOne(df => df.Servicio).WithMany(s => s.DetalleFacturas)
                .HasForeignKey(df => df.IdServicio).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_DetalleFactura_Servicios");
            // Regla 11: la pareja factura-servicio no puede repetirse.
            e.HasIndex(df => new { df.IdFactura, df.IdServicio }).IsUnique()
                .HasDatabaseName("UQ_DetalleFactura_Factura_Servicio");
            e.HasIndex(df => df.IdServicio).HasDatabaseName("IX_DetalleFactura_IdServicio");
            e.HasCheckConstraint("CK_DetalleFactura_Subtotal", "Subtotal >= 0");
        });
    }
}
