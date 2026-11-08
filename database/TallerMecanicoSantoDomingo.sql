
CREATE DATABASE TallerMecanicoSantoDomingo;
GO

USE TallerMecanicoSantoDomingo;
GO

CREATE TABLE Cliente (
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
ON Cliente(Cedula)
WHERE Cedula IS NOT NULL;

CREATE TABLE Vehiculo (
    IdVehiculo int IDENTITY(1,1) PRIMARY KEY,
    IdCliente int NOT NULL REFERENCES Cliente(IdCliente),
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
ON Vehiculo(NumChasis)
WHERE NumChasis IS NOT NULL;

CREATE TABLE Trabajador (
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

CREATE TABLE Servicio (
    IdServicio int IDENTITY(1,1) PRIMARY KEY,
    IdVehiculo int NOT NULL REFERENCES Vehiculo(IdVehiculo),
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

CREATE TABLE Detalle_Servicio (
    IdServicio int NOT NULL REFERENCES Servicio(IdServicio),
    IdTrabajador int NOT NULL REFERENCES Trabajador(IdTrabajador),
    FechaAsignacion datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    Estado nvarchar(20) NOT NULL DEFAULT 'Activo',
    Observaciones nvarchar(200) NULL,
    PRIMARY KEY (IdServicio, IdTrabajador)
);

CREATE TABLE Producto (
    IdProducto int IDENTITY(1,1) PRIMARY KEY,
    Nombre nvarchar(100) NOT NULL,
    Precio decimal(12,2) NOT NULL,
    Costo decimal(12,2) NOT NULL DEFAULT 0,
    Descripcion nvarchar(300) NOT NULL,
    Stock decimal(12,3) NOT NULL,
    Activo bit NOT NULL DEFAULT 1,
    FechaRegistro datetime2 NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE TABLE Productos_Utilizados (
    IdProducto int NOT NULL REFERENCES Producto(IdProducto),
    IdServicio int NOT NULL REFERENCES Servicio(IdServicio),
    Cantidad decimal(12,3) NOT NULL,
    PRIMARY KEY (IdProducto, IdServicio)
);

CREATE TABLE Factura (
    IdFactura int IDENTITY(1,1) PRIMARY KEY,
    IdCliente int NOT NULL REFERENCES Cliente(IdCliente),
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

CREATE TABLE Detalle_Factura (
    IdServicio int NOT NULL REFERENCES Servicio(IdServicio),
    IdFactura int NOT NULL REFERENCES Factura(IdFactura),
    Subtotal decimal(12,2) NOT NULL,
    PRIMARY KEY (IdServicio, IdFactura)
);

CREATE TABLE Usuarios (
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
ON Usuarios(NormalizedUserName)
WHERE NormalizedUserName IS NOT NULL;

CREATE UNIQUE INDEX UQ_Usuarios_Correo
ON Usuarios(NormalizedEmail)
WHERE NormalizedEmail IS NOT NULL;
GO
