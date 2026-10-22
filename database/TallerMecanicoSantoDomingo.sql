-- Crear una base nueva. Este archivo se ejecuta una sola vez.
CREATE DATABASE TallerMecanicoSantoDomingo;
GO

USE TallerMecanicoSantoDomingo;
GO

CREATE TABLE Cliente (
    IdCliente int PRIMARY KEY IDENTITY(1,1),
    Cedula nvarchar(20) NULL,
    Nombre nvarchar(100) NOT NULL,
    Apellido nvarchar(100) NOT NULL,
    Telefono nvarchar(20) NOT NULL,
    RUC nvarchar(20) NOT NULL UNIQUE,
    Direccion nvarchar(200) NOT NULL,
    Activo bit NOT NULL DEFAULT 1,
    FechaRegistro datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_Cliente_Nombre CHECK (LTRIM(Nombre) <> ''),
    CONSTRAINT CK_Cliente_Apellido CHECK (LTRIM(Apellido) <> '')
);

CREATE UNIQUE INDEX UQ_Cliente_Cedula ON Cliente(Cedula) WHERE Cedula IS NOT NULL;

GO

CREATE TABLE Vehiculo (
    IdVehiculo int PRIMARY KEY IDENTITY(1,1),
    IdCliente int NOT NULL,
    Marca nvarchar(50) NOT NULL,
    Modelo nvarchar(50) NOT NULL,
    Anio int NOT NULL,
    Kilometraje int NOT NULL DEFAULT 0,
    Placa nvarchar(20) NOT NULL UNIQUE,
    Color nvarchar(50) NULL,
    NumChasis nvarchar(50) NULL,
    Tipo nvarchar(50) NULL,
    Activo bit NOT NULL DEFAULT 1,
    FechaRegistro datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_Vehiculo_Anio CHECK (Anio BETWEEN 1950 AND YEAR(GETDATE()) + 1),
    CONSTRAINT CK_Vehiculo_Kilometraje CHECK (Kilometraje >= 0),
    FOREIGN KEY (IdCliente) REFERENCES Cliente(IdCliente)
);

CREATE UNIQUE INDEX UQ_Vehiculo_Chasis ON Vehiculo(NumChasis) WHERE NumChasis IS NOT NULL;

GO

CREATE TABLE Trabajador (
    IdTrabajador int PRIMARY KEY IDENTITY(1,1),
    Nombre nvarchar(100) NOT NULL,
    Apellido nvarchar(100) NOT NULL,
    Estado bit NOT NULL DEFAULT 1,
    Correo nvarchar(256) NULL,
    FechaIngreso datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    FechaSalida date NULL,
    Telefono nvarchar(20) NOT NULL,
    Area nvarchar(50) NOT NULL,
    CONSTRAINT CK_Trabajador_Area CHECK (Area IN (N'Mantenimiento mecánico', N'Pintura')),
    CONSTRAINT CK_Trabajador_Fechas CHECK (FechaSalida IS NULL OR FechaSalida >= CAST(FechaIngreso AS date))
);

GO

CREATE TABLE Servicio (
    IdServicio int PRIMARY KEY IDENTITY(1,1),
    IdVehiculo int NOT NULL,
    FechaInicio date NOT NULL,
    FechaFin date NULL,
    Costo decimal(12,2) NOT NULL DEFAULT 0,
    Descripcion nvarchar(500) NOT NULL,
    Tipo nvarchar(20) NOT NULL,
    Estado nvarchar(20) NOT NULL DEFAULT 'Abierto',
    DescripcionFalla nvarchar(500) NULL,
    TipoPintura nvarchar(50) NULL,
    Color nvarchar(50) NULL,
    FechaRegistro datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_Servicio_Tipo CHECK (Tipo IN ('Mantenimiento', 'Pintura')),
    CONSTRAINT CK_Servicio_Estado CHECK (Estado IN ('Abierto', 'Finalizado', 'Cancelado')),
    CONSTRAINT CK_Servicio_FechaFin CHECK (FechaFin IS NULL OR FechaFin >= FechaInicio),
    CONSTRAINT CK_Servicio_Costo CHECK (Costo >= 0),
    FOREIGN KEY (IdVehiculo) REFERENCES Vehiculo(IdVehiculo)
);

GO

CREATE TABLE Detalle_Servicio (
    IdServicio int NOT NULL,
    IdTrabajador int NOT NULL,
    FechaAsignacion datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    Estado nvarchar(20) NOT NULL DEFAULT 'Activo',
    Observaciones nvarchar(200) NULL,
    PRIMARY KEY (IdServicio, IdTrabajador),
    CONSTRAINT CK_DetalleServicio_Estado CHECK (Estado IN ('Activo', 'Retirado')),
    FOREIGN KEY (IdServicio) REFERENCES Servicio(IdServicio),
    FOREIGN KEY (IdTrabajador) REFERENCES Trabajador(IdTrabajador)
);

GO

CREATE TABLE Producto (
    IdProducto int PRIMARY KEY IDENTITY(1,1),
    Nombre nvarchar(100) NOT NULL,
    Precio decimal(12,2) NOT NULL,
    Costo decimal(12,2) NOT NULL DEFAULT 0,
    Descripcion nvarchar(300) NOT NULL,
    Stock decimal(12,3) NOT NULL,
    Activo bit NOT NULL DEFAULT 1,
    FechaRegistro datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_Producto_Precio CHECK (Precio >= 0),
    CONSTRAINT CK_Producto_Costo CHECK (Costo >= 0),
    CONSTRAINT CK_Producto_Stock CHECK (Stock >= 0)
);

GO

CREATE TABLE Productos_Utilizados (
    IdProducto int NOT NULL,
    IdServicio int NOT NULL,
    Cantidad decimal(12,3) NOT NULL,
    PRIMARY KEY (IdProducto, IdServicio),
    CONSTRAINT CK_ProductosUtilizados_Cantidad CHECK (Cantidad > 0),
    FOREIGN KEY (IdProducto) REFERENCES Producto(IdProducto),
    FOREIGN KEY (IdServicio) REFERENCES Servicio(IdServicio)
);

GO

CREATE TABLE Factura (
    IdFactura int PRIMARY KEY IDENTITY(1,1),
    Fecha datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    Total decimal(12,2) NOT NULL,
    RUC nvarchar(20) NULL,
    IVA decimal(12,2) NOT NULL,
    IdCliente int NOT NULL,
    NumeroFactura nvarchar(20) NOT NULL UNIQUE,
    Subtotal decimal(12,2) NOT NULL,
    MetodoPago nvarchar(30) NOT NULL,
    Estado nvarchar(20) NOT NULL DEFAULT 'Emitida',
    Notas nvarchar(200) NULL,
    CONSTRAINT CK_Factura_MetodoPago CHECK (MetodoPago IN ('Efectivo', 'Tarjeta', 'Transferencia')),
    CONSTRAINT CK_Factura_Estado CHECK (Estado IN ('Emitida', 'Anulada')),
    CONSTRAINT CK_Factura_Importes CHECK (Subtotal >= 0 AND IVA >= 0 AND Total = Subtotal + IVA),
    FOREIGN KEY (IdCliente) REFERENCES Cliente(IdCliente)
);

GO

CREATE TABLE Detalle_Factura (
    IdServicio int NOT NULL,
    IdFactura int NOT NULL,
    Subtotal decimal(12,2) NOT NULL,
    PRIMARY KEY (IdServicio, IdFactura),
    CONSTRAINT CK_DetalleFactura_Subtotal CHECK (Subtotal >= 0),
    FOREIGN KEY (IdServicio) REFERENCES Servicio(IdServicio),
    FOREIGN KEY (IdFactura) REFERENCES Factura(IdFactura)
);

GO

CREATE TABLE Usuarios (
    Id nvarchar(450) NOT NULL PRIMARY KEY,
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

CREATE UNIQUE INDEX UQ_Usuarios_Nombre ON Usuarios(NormalizedUserName) WHERE NormalizedUserName IS NOT NULL;

CREATE UNIQUE INDEX UQ_Usuarios_Correo ON Usuarios(NormalizedEmail) WHERE NormalizedEmail IS NOT NULL;

GO

-- Anular conserva la factura y sus detalles.
CREATE OR ALTER PROCEDURE dbo.usp_AnularFactura
    @IdFactura int,
    @Motivo nvarchar(200) = NULL
AS
BEGIN
    SET NOCOUNT OFF;
    UPDATE dbo.Factura
    SET Estado = 'Anulada',
        Notas = CASE WHEN NULLIF(LTRIM(RTRIM(@Motivo)), '') IS NULL THEN ISNULL(Notas, '')
                     ELSE LEFT(N'Anulada: ' + LTRIM(RTRIM(@Motivo)), 200) END
    WHERE IdFactura = @IdFactura AND Estado = 'Emitida';
    RETURN @@ROWCOUNT;
END;
GO
