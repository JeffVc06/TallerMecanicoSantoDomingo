-- Crear una base nueva. Ejecutar este archivo una sola vez.
CREATE DATABASE TallerMecanicoSantoDomingo;
GO

USE TallerMecanicoSantoDomingo;
GO

CREATE TABLE dbo.Cliente (
    IdCliente int IDENTITY(1,1) PRIMARY KEY,
    Cedula nvarchar(20) NULL,
    Nombre nvarchar(100) NOT NULL,
    Apellido nvarchar(100) NOT NULL,
    Telefono nvarchar(20) NOT NULL,
    RUC nvarchar(20) NOT NULL UNIQUE,
    Direccion nvarchar(200) NOT NULL,
    Activo bit NOT NULL DEFAULT 1,
    FechaRegistro datetime2 NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE UNIQUE INDEX UQ_Cliente_Cedula
ON dbo.Cliente(Cedula)
WHERE Cedula IS NOT NULL;

CREATE TABLE dbo.Vehiculo (
    IdVehiculo int IDENTITY(1,1) PRIMARY KEY,
    IdCliente int NOT NULL REFERENCES dbo.Cliente(IdCliente),
    Marca nvarchar(50) NOT NULL,
    Modelo nvarchar(50) NOT NULL,
    Anio int NOT NULL,
    Kilometraje int NOT NULL DEFAULT 0,
    Placa nvarchar(20) NOT NULL UNIQUE,
    Color nvarchar(50) NULL,
    NumChasis nvarchar(50) NULL,
    Tipo nvarchar(50) NULL,
    Activo bit NOT NULL DEFAULT 1,
    FechaRegistro datetime2 NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE UNIQUE INDEX UQ_Vehiculo_Chasis
ON dbo.Vehiculo(NumChasis)
WHERE NumChasis IS NOT NULL;

CREATE TABLE dbo.Trabajador (
    IdTrabajador int IDENTITY(1,1) PRIMARY KEY,
    Nombre nvarchar(100) NOT NULL,
    Apellido nvarchar(100) NOT NULL,
    Estado bit NOT NULL DEFAULT 1,
    Correo nvarchar(256) NULL,
    FechaIngreso datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    FechaSalida date NULL,
    Telefono nvarchar(20) NOT NULL,
    Area nvarchar(50) NOT NULL
);

CREATE TABLE dbo.Servicio (
    IdServicio int IDENTITY(1,1) PRIMARY KEY,
    IdVehiculo int NOT NULL REFERENCES dbo.Vehiculo(IdVehiculo),
    FechaInicio date NOT NULL,
    FechaFin date NULL,
    Costo decimal(12,2) NOT NULL DEFAULT 0,
    Descripcion nvarchar(500) NOT NULL,
    Tipo nvarchar(20) NOT NULL,
    Estado nvarchar(20) NOT NULL DEFAULT 'Abierto',
    DescripcionFalla nvarchar(500) NULL,
    TipoPintura nvarchar(50) NULL,
    Color nvarchar(50) NULL,
    FechaRegistro datetime2 NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE TABLE dbo.Detalle_Servicio (
    IdServicio int NOT NULL REFERENCES dbo.Servicio(IdServicio),
    IdTrabajador int NOT NULL REFERENCES dbo.Trabajador(IdTrabajador),
    FechaAsignacion datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    Estado nvarchar(20) NOT NULL DEFAULT 'Activo',
    Observaciones nvarchar(200) NULL,
    PRIMARY KEY (IdServicio, IdTrabajador)
);

CREATE TABLE dbo.Producto (
    IdProducto int IDENTITY(1,1) PRIMARY KEY,
    Nombre nvarchar(100) NOT NULL,
    Precio decimal(12,2) NOT NULL,
    Costo decimal(12,2) NOT NULL DEFAULT 0,
    Descripcion nvarchar(300) NOT NULL,
    Stock decimal(12,3) NOT NULL,
    Activo bit NOT NULL DEFAULT 1,
    FechaRegistro datetime2 NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE TABLE dbo.Productos_Utilizados (
    IdProducto int NOT NULL REFERENCES dbo.Producto(IdProducto),
    IdServicio int NOT NULL REFERENCES dbo.Servicio(IdServicio),
    Cantidad decimal(12,3) NOT NULL,
    PRIMARY KEY (IdProducto, IdServicio)
);

CREATE TABLE dbo.Factura (
    IdFactura int IDENTITY(1,1) PRIMARY KEY,
    IdCliente int NOT NULL REFERENCES dbo.Cliente(IdCliente),
    Fecha datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    Total decimal(12,2) NOT NULL,
    RUC nvarchar(20) NULL,
    IVA decimal(12,2) NOT NULL,
    NumeroFactura nvarchar(20) NOT NULL UNIQUE,
    Subtotal decimal(12,2) NOT NULL,
    MetodoPago nvarchar(30) NOT NULL,
    Estado nvarchar(20) NOT NULL DEFAULT 'Emitida',
    Notas nvarchar(200) NULL
);

CREATE TABLE dbo.Detalle_Factura (
    IdServicio int NOT NULL REFERENCES dbo.Servicio(IdServicio),
    IdFactura int NOT NULL REFERENCES dbo.Factura(IdFactura),
    Subtotal decimal(12,2) NOT NULL,
    PRIMARY KEY (IdServicio, IdFactura)
);

CREATE TABLE dbo.Usuarios (
    Id nvarchar(450) PRIMARY KEY,
    UserName nvarchar(256) NULL,
    NormalizedUserName nvarchar(256) NULL,
    Email nvarchar(256) NULL,
    NormalizedEmail nvarchar(256) NULL,
    EmailConfirmed bit NOT NULL DEFAULT 0,
    PasswordHash nvarchar(max) NULL,
    SecurityStamp nvarchar(max) NULL,
    ConcurrencyStamp nvarchar(max) NULL,
    LockoutEnd datetimeoffset NULL,
    LockoutEnabled bit NOT NULL DEFAULT 1,
    AccessFailedCount int NOT NULL DEFAULT 0
);

CREATE UNIQUE INDEX UQ_Usuarios_Nombre
ON dbo.Usuarios(NormalizedUserName)
WHERE NormalizedUserName IS NOT NULL;

CREATE UNIQUE INDEX UQ_Usuarios_Correo
ON dbo.Usuarios(NormalizedEmail)
WHERE NormalizedEmail IS NOT NULL;
GO

-- Validaciones que también se pueden aplicar a una base creada con el esquema anterior.
IF OBJECT_ID(N'dbo.CK_Cliente_Nombre', N'C') IS NULL
    ALTER TABLE dbo.Cliente WITH CHECK ADD CONSTRAINT CK_Cliente_Nombre CHECK (LTRIM(Nombre) <> '');
IF OBJECT_ID(N'dbo.CK_Cliente_Apellido', N'C') IS NULL
    ALTER TABLE dbo.Cliente WITH CHECK ADD CONSTRAINT CK_Cliente_Apellido CHECK (LTRIM(Apellido) <> '');

IF OBJECT_ID(N'dbo.CK_Vehiculo_Anio', N'C') IS NULL
    ALTER TABLE dbo.Vehiculo WITH CHECK ADD CONSTRAINT CK_Vehiculo_Anio CHECK (Anio BETWEEN 1950 AND YEAR(GETDATE()) + 1);
IF OBJECT_ID(N'dbo.CK_Vehiculo_Kilometraje', N'C') IS NULL
    ALTER TABLE dbo.Vehiculo WITH CHECK ADD CONSTRAINT CK_Vehiculo_Kilometraje CHECK (Kilometraje >= 0);

IF OBJECT_ID(N'dbo.CK_Trabajador_Area', N'C') IS NULL
    ALTER TABLE dbo.Trabajador WITH CHECK ADD CONSTRAINT CK_Trabajador_Area CHECK (Area IN (N'Mantenimiento mecánico', N'Pintura'));
IF OBJECT_ID(N'dbo.CK_Trabajador_Fechas', N'C') IS NULL
    ALTER TABLE dbo.Trabajador WITH CHECK ADD CONSTRAINT CK_Trabajador_Fechas CHECK (FechaSalida IS NULL OR FechaSalida >= CAST(FechaIngreso AS date));

IF OBJECT_ID(N'dbo.CK_Servicio_Tipo', N'C') IS NULL
    ALTER TABLE dbo.Servicio WITH CHECK ADD CONSTRAINT CK_Servicio_Tipo CHECK (Tipo IN (N'Mantenimiento', N'Pintura'));
IF OBJECT_ID(N'dbo.CK_Servicio_Estado', N'C') IS NULL
    ALTER TABLE dbo.Servicio WITH CHECK ADD CONSTRAINT CK_Servicio_Estado CHECK (Estado IN (N'Abierto', N'Finalizado', N'Cancelado'));
IF OBJECT_ID(N'dbo.CK_Servicio_FechaFin', N'C') IS NULL
    ALTER TABLE dbo.Servicio WITH CHECK ADD CONSTRAINT CK_Servicio_FechaFin CHECK (FechaFin IS NULL OR FechaFin >= FechaInicio);
IF OBJECT_ID(N'dbo.CK_Servicio_Costo', N'C') IS NULL
    ALTER TABLE dbo.Servicio WITH CHECK ADD CONSTRAINT CK_Servicio_Costo CHECK (Costo >= 0);

IF OBJECT_ID(N'dbo.CK_DetalleServicio_Estado', N'C') IS NULL
    ALTER TABLE dbo.Detalle_Servicio WITH CHECK ADD CONSTRAINT CK_DetalleServicio_Estado CHECK (Estado IN (N'Activo', N'Retirado'));

IF OBJECT_ID(N'dbo.CK_Producto_Precio', N'C') IS NULL
    ALTER TABLE dbo.Producto WITH CHECK ADD CONSTRAINT CK_Producto_Precio CHECK (Precio >= 0);
IF OBJECT_ID(N'dbo.CK_Producto_Costo', N'C') IS NULL
    ALTER TABLE dbo.Producto WITH CHECK ADD CONSTRAINT CK_Producto_Costo CHECK (Costo >= 0);
IF OBJECT_ID(N'dbo.CK_Producto_Stock', N'C') IS NULL
    ALTER TABLE dbo.Producto WITH CHECK ADD CONSTRAINT CK_Producto_Stock CHECK (Stock >= 0);

IF OBJECT_ID(N'dbo.CK_ProductosUtilizados_Cantidad', N'C') IS NULL
    ALTER TABLE dbo.Productos_Utilizados WITH CHECK ADD CONSTRAINT CK_ProductosUtilizados_Cantidad CHECK (Cantidad > 0);

IF OBJECT_ID(N'dbo.CK_Factura_MetodoPago', N'C') IS NULL
    ALTER TABLE dbo.Factura WITH CHECK ADD CONSTRAINT CK_Factura_MetodoPago CHECK (MetodoPago IN (N'Efectivo', N'Tarjeta', N'Transferencia'));
IF OBJECT_ID(N'dbo.CK_Factura_Estado', N'C') IS NULL
    ALTER TABLE dbo.Factura WITH CHECK ADD CONSTRAINT CK_Factura_Estado CHECK (Estado IN (N'Emitida', N'Anulada'));
IF OBJECT_ID(N'dbo.CK_Factura_Importes', N'C') IS NULL
    ALTER TABLE dbo.Factura WITH CHECK ADD CONSTRAINT CK_Factura_Importes CHECK (Subtotal >= 0 AND IVA >= 0 AND Total = Subtotal + IVA);

IF OBJECT_ID(N'dbo.CK_DetalleFactura_Subtotal', N'C') IS NULL
    ALTER TABLE dbo.Detalle_Factura WITH CHECK ADD CONSTRAINT CK_DetalleFactura_Subtotal CHECK (Subtotal >= 0);
GO

-- Anular conserva la factura y sus detalles.
CREATE OR ALTER PROCEDURE dbo.usp_AnularFactura
    @IdFactura int,
    @Motivo nvarchar(200) = NULL
AS
BEGIN
    SET NOCOUNT OFF;
    UPDATE dbo.Factura
    SET Estado = N'Anulada',
        Notas = CASE WHEN NULLIF(LTRIM(RTRIM(@Motivo)), N'') IS NULL THEN ISNULL(Notas, N'')
                     ELSE LEFT(N'Anulada: ' + LTRIM(RTRIM(@Motivo)), 200) END
    WHERE IdFactura = @IdFactura AND Estado = N'Emitida';
    RETURN @@ROWCOUNT;
END;
GO
