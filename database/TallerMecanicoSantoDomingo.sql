/* ==========================================================================================
   TALLER MECÁNICO SANTO DOMINGO — ESQUEMA DE BASE DE DATOS (SQL Server)
   ------------------------------------------------------------------------------------------
   Proyecto      : Sistema web ASP.NET Core MVC + SQL Server
   Motor         : Microsoft SQL Server (compatible con SQL Server 2016 SP1 o superior)
   Base de datos : TallerMecanicoSantoDomingo

   Este archivo es la FUENTE ÚNICA DEL ESQUEMA. La aplicación ASP.NET Core (Entity Framework
   Core) NO usa migraciones: su mapeo (Data/TallerDbContext.cs) replica exactamente las
   tablas, columnas, claves e índices definidos aquí y la aplicación aplica este script de
   forma automática en el primer arranque si la base de datos todavía no tiene las tablas.

   Uso manual (Windows integrado):
       sqlcmd -S localhost -E -C -i "database\TallerMecanicoSantoDomingo.sql"
   Uso manual (usuario y contraseña):
       sqlcmd -S localhost -U sa -P "TU_CONTRASEÑA" -i "database\TallerMecanicoSantoDomingo.sql"

   CONTENIDO
     0) Creación de la base de datos
     1) Tablas de seguridad de ASP.NET Core Identity (AspNet*)
     2) Entidades del MER: Clientes, Vehiculos, Areas, Trabajadores, Servicios,
        DetalleServicio, Productos, ProductosUtilizados, Facturas, DetalleFactura
     3) Datos iniciales del catálogo de Áreas de trabajo
     4) Procedimiento almacenado usp_AnularFactura (Regla 14)
     5) Consultas de verificación del esquema y de las 14 reglas de negocio

   EXTENSIONES TÉCNICAS (atributos agregados que NO aparecen en el documento original;
   se marcan para que quede constancia y no se confundan con el diseño del MER):
     - Activo y FechaRegistro en Clientes, Vehiculos, Trabajadores y Productos
       (desactivación lógica para no borrar historial, Regla 14).
     - NumeroFactura, Subtotal e IVA en Facturas (Regla 13 exige registrar el IVA).
     - IdDetalleServicio / IdProductoUtilizado / IdDetalleFactura como claves sustitutas
       de las tablas puente del MER.
   ========================================================================================== */


/* ------------------------------------------------------------------------------------------
   0) CREACIÓN DE LA BASE DE DATOS
   ------------------------------------------------------------------------------------------ */
IF DB_ID(N'TallerMecanicoSantoDomingo') IS NULL
BEGIN
    CREATE DATABASE [TallerMecanicoSantoDomingo];
END
GO

USE [TallerMecanicoSantoDomingo];
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO


/* ------------------------------------------------------------------------------------------
   1) TABLAS DE IDENTIDAD DE ASP.NET CORE IDENTITY
   Nombres, tipos y longitudes estándar de IdentityDbContext (IdentityUser sin personalizar).
   ------------------------------------------------------------------------------------------ */

IF OBJECT_ID(N'dbo.AspNetRoles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AspNetRoles
    (
        Id               NVARCHAR(450) NOT NULL,
        Name             NVARCHAR(256) NULL,
        NormalizedName   NVARCHAR(256) NULL,
        ConcurrencyStamp NVARCHAR(MAX) NULL,
        CONSTRAINT PK_AspNetRoles PRIMARY KEY (Id)
    );
    CREATE UNIQUE INDEX RoleNameIndex ON dbo.AspNetRoles (NormalizedName)
        WHERE NormalizedName IS NOT NULL;
END
GO

IF OBJECT_ID(N'dbo.AspNetUsers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AspNetUsers
    (
        Id                   NVARCHAR(450) NOT NULL,
        UserName             NVARCHAR(256) NULL,
        NormalizedUserName   NVARCHAR(256) NULL,
        Email                NVARCHAR(256) NULL,
        NormalizedEmail      NVARCHAR(256) NULL,
        EmailConfirmed       BIT NOT NULL,
        PasswordHash         NVARCHAR(MAX) NULL,
        SecurityStamp        NVARCHAR(MAX) NULL,
        ConcurrencyStamp     NVARCHAR(MAX) NULL,
        PhoneNumber          NVARCHAR(MAX) NULL,
        PhoneNumberConfirmed BIT NOT NULL,
        TwoFactorEnabled     BIT NOT NULL,
        LockoutEnd           DATETIMEOFFSET NULL,
        LockoutEnabled       BIT NOT NULL,
        AccessFailedCount    INT NOT NULL,
        CONSTRAINT PK_AspNetUsers PRIMARY KEY (Id)
    );
    CREATE INDEX EmailIndex ON dbo.AspNetUsers (NormalizedEmail);
    CREATE UNIQUE INDEX UserNameIndex ON dbo.AspNetUsers (NormalizedUserName)
        WHERE NormalizedUserName IS NOT NULL;
END
GO

IF OBJECT_ID(N'dbo.AspNetRoleClaims', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AspNetRoleClaims
    (
        Id         INT IDENTITY(1,1) NOT NULL,
        RoleId     NVARCHAR(450) NOT NULL,
        ClaimType  NVARCHAR(MAX) NULL,
        ClaimValue NVARCHAR(MAX) NULL,
        CONSTRAINT PK_AspNetRoleClaims PRIMARY KEY (Id),
        CONSTRAINT FK_AspNetRoleClaims_AspNetRoles_RoleId
            FOREIGN KEY (RoleId) REFERENCES dbo.AspNetRoles (Id) ON DELETE CASCADE
    );
    CREATE INDEX IX_AspNetRoleClaims_RoleId ON dbo.AspNetRoleClaims (RoleId);
END
GO

IF OBJECT_ID(N'dbo.AspNetUserClaims', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AspNetUserClaims
    (
        Id         INT IDENTITY(1,1) NOT NULL,
        UserId     NVARCHAR(450) NOT NULL,
        ClaimType  NVARCHAR(MAX) NULL,
        ClaimValue NVARCHAR(MAX) NULL,
        CONSTRAINT PK_AspNetUserClaims PRIMARY KEY (Id),
        CONSTRAINT FK_AspNetUserClaims_AspNetUsers FOREIGN KEY (UserId)
            REFERENCES dbo.AspNetUsers (Id) ON DELETE CASCADE
    );
    CREATE INDEX IX_AspNetUserClaims_UserId ON dbo.AspNetUserClaims (UserId);
END
GO

IF OBJECT_ID(N'dbo.AspNetUserLogins', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AspNetUserLogins
    (
        LoginProvider       NVARCHAR(450) NOT NULL,
        ProviderKey         NVARCHAR(450) NOT NULL,
        ProviderDisplayName NVARCHAR(MAX) NULL,
        UserId              NVARCHAR(450) NOT NULL,
        CONSTRAINT PK_AspNetUserLogins PRIMARY KEY (LoginProvider, ProviderKey),
        CONSTRAINT FK_AspNetUserLogins_AspNetUsers_UserId
            FOREIGN KEY (UserId) REFERENCES dbo.AspNetUsers (Id) ON DELETE CASCADE
    );
    CREATE INDEX IX_AspNetUserLogins_UserId ON dbo.AspNetUserLogins (UserId);
END
GO

IF OBJECT_ID(N'dbo.AspNetUserRoles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AspNetUserRoles
    (
        UserId NVARCHAR(450) NOT NULL,
        RoleId NVARCHAR(450) NOT NULL,
        CONSTRAINT PK_AspNetUserRoles PRIMARY KEY (UserId, RoleId),
        CONSTRAINT FK_AspNetUserRoles_AspNetRoles_RoleId
            FOREIGN KEY (RoleId) REFERENCES dbo.AspNetRoles (Id) ON DELETE CASCADE,
        CONSTRAINT FK_AspNetUserRoles_AspNetUsers_UserId
            FOREIGN KEY (UserId) REFERENCES dbo.AspNetUsers (Id) ON DELETE CASCADE
    );
    CREATE INDEX IX_AspNetUserRoles_RoleId ON dbo.AspNetUserRoles (RoleId);
END
GO

IF OBJECT_ID(N'dbo.AspNetUserTokens', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AspNetUserTokens
    (
        UserId        NVARCHAR(450) NOT NULL,
        LoginProvider NVARCHAR(450) NOT NULL,
        Name          NVARCHAR(450) NOT NULL,
        Value         NVARCHAR(MAX) NULL,
        CONSTRAINT PK_AspNetUserTokens PRIMARY KEY (UserId, LoginProvider, Name),
        CONSTRAINT FK_AspNetUserTokens_AspNetUsers_UserId
            FOREIGN KEY (UserId) REFERENCES dbo.AspNetUsers (Id) ON DELETE CASCADE
    );
END
GO


/* ==========================================================================================
   2) ENTIDADES DEL MODELO ENTIDAD-RELACIÓN
   ========================================================================================== */

/* ----- CLIENTES (MER: CLIENTE) ------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.Clientes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Clientes
    (
        IdCliente     INT IDENTITY(1,1) NOT NULL,
        Nombre        NVARCHAR(100) NOT NULL,
        Apellido      NVARCHAR(100) NOT NULL,
        Telefono      NVARCHAR(20)  NOT NULL,
        Direccion     NVARCHAR(200) NOT NULL,
        RFC           NVARCHAR(20)  NOT NULL,
        Activo        BIT           NOT NULL CONSTRAINT DF_Clientes_Activo      DEFAULT (1),
        FechaRegistro DATETIME2(0)  NOT NULL CONSTRAINT DF_Clientes_FechaRegistro DEFAULT (SYSUTCDATETIME()),

        CONSTRAINT PK_Clientes PRIMARY KEY (IdCliente),
        CONSTRAINT UQ_Clientes_RFC UNIQUE (RFC),
        -- Regla 1: nombre y apellido no pueden quedar vacíos.
        CONSTRAINT CK_Clientes_Nombre_NoVacio   CHECK (LTRIM(Nombre)   <> ''),
        CONSTRAINT CK_Clientes_Apellido_NoVacio CHECK (LTRIM(Apellido) <> '')
    );
    CREATE INDEX IX_Clientes_Nombre_Apellido ON dbo.Clientes (Nombre, Apellido);
    CREATE INDEX IX_Clientes_Telefono        ON dbo.Clientes (Telefono);
END
GO

/* ------------------------------------------------------------------------------------------
   2.1) VEHICULOS  (relación 1:N con Clientes — Regla 2 y Regla 3)
   ------------------------------------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.Vehiculos', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Vehiculos
    (
        IdVehiculo    INT IDENTITY(1,1) NOT NULL,
        IdCliente     INT           NOT NULL,
        Placa         NVARCHAR(20)  NOT NULL,
        Marca         NVARCHAR(50)  NOT NULL,
        Modelo        NVARCHAR(50)  NOT NULL,
        Anio          INT           NOT NULL,
        Activo        BIT           NOT NULL CONSTRAINT DF_Vehiculos_Activo        DEFAULT (1),
        FechaRegistro DATETIME2(0)  NOT NULL CONSTRAINT DF_Vehiculos_FechaRegistro DEFAULT (SYSUTCDATETIME()),

        CONSTRAINT PK_Vehiculos PRIMARY KEY (IdVehiculo),
        CONSTRAINT UQ_Vehiculos_Placa UNIQUE (Placa),
        -- Regla 2: cada vehículo pertenece a un único cliente.
        CONSTRAINT FK_Vehiculos_Clientes FOREIGN KEY (IdCliente)
            REFERENCES dbo.Clientes (IdCliente),
        CONSTRAINT CK_Vehiculos_Anio CHECK (Anio >= 1950 AND Anio <= YEAR(GETDATE()) + 1)
    );
    CREATE INDEX IX_Vehiculos_IdCliente ON dbo.Vehiculos (IdCliente);
END
GO

/* ------------------------------------------------------------------------------------------
   2.2) AREAS DE TRABAJO (catálogo del MER: ÁREA DE TRABAJO)
   ------------------------------------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.Areas', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Areas
    (
        IdArea INT IDENTITY(1,1) NOT NULL,
        Nombre NVARCHAR(50) NOT NULL,

        CONSTRAINT PK_Areas PRIMARY KEY (IdArea),
        CONSTRAINT UQ_Areas_Nombre UNIQUE (Nombre)
    );
END
GO

/* ------------------------------------------------------------------------------------------
   2.3) TRABAJADORES (relación N:1 con Areas — Regla 5)
   ------------------------------------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.Trabajadores', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Trabajadores
    (
        IdTrabajador  INT IDENTITY(1,1) NOT NULL,
        Nombre        NVARCHAR(100) NOT NULL,
        Apellido      NVARCHAR(100) NOT NULL,
        Telefono      NVARCHAR(20)  NOT NULL,
        IdArea        INT           NOT NULL,
        Activo        BIT           NOT NULL CONSTRAINT DF_Trabajadores_Activo        DEFAULT (1),
        FechaRegistro DATETIME2(0)  NOT NULL CONSTRAINT DF_Trabajadores_FechaRegistro DEFAULT (SYSUTCDATETIME()),

        CONSTRAINT PK_Trabajadores PRIMARY KEY (IdTrabajador),
        -- Regla 5: cada trabajador pertenece a una sola área de trabajo.
        CONSTRAINT FK_Trabajadores_Areas FOREIGN KEY (IdArea)
            REFERENCES dbo.Areas (IdArea)
    );
    CREATE INDEX IX_Trabajadores_IdArea ON dbo.Trabajadores (IdArea);
END
GO

/* ------------------------------------------------------------------------------------------
   2.4) SERVICIOS (unifica MANTENIMIENTO MECÁNICO y TRABAJO DE PINTURA — Reglas 4 y 8)
   ------------------------------------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.Servicios', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Servicios
    (
        IdServicio       INT IDENTITY(1,1) NOT NULL,
        IdVehiculo       INT           NOT NULL,
        TipoServicio     NVARCHAR(20)  NOT NULL,   -- Mantenimiento | Pintura
        Descripcion      NVARCHAR(500) NOT NULL,
        DescripcionFalla NVARCHAR(500) NULL,       -- propio del mantenimiento mecánico
        TipoPintura      NVARCHAR(50)  NULL,
        Color            NVARCHAR(50)  NULL,
        Estado           NVARCHAR(20)  NOT NULL CONSTRAINT DF_Servicios_Estado DEFAULT ('Abierto'),
        FechaInicio      DATE          NOT NULL,
        FechaFin         DATE          NULL,
        Total            DECIMAL(12,2) NOT NULL CONSTRAINT DF_Servicios_Total DEFAULT (0),
        FechaRegistro    DATETIME2(0)  NOT NULL CONSTRAINT DF_Servicios_FechaRegistro DEFAULT (SYSUTCDATETIME()),

        CONSTRAINT PK_Servicios PRIMARY KEY (IdServicio),
        -- Regla 3: cada servicio corresponde a un único vehículo.
        CONSTRAINT FK_Servicios_Vehiculos FOREIGN KEY (IdVehiculo)
            REFERENCES dbo.Vehiculos (IdVehiculo),
        -- Regla 4: el servicio solo puede ser de tipo Mantenimiento o Pintura.
        CONSTRAINT CK_Servicios_TipoServicio CHECK (TipoServicio IN ('Mantenimiento', 'Pintura')),
        -- Reglas 4 y 8: estados permitidos.
        CONSTRAINT CK_Servicios_Estado CHECK (Estado IN ('Abierto', 'Finalizado', 'Cancelado')),
        -- Regla 8: la fecha de fin no puede ser anterior a la fecha de inicio.
        CONSTRAINT CK_Servicios_FechaFin CHECK (FechaFin IS NULL OR FechaFin >= FechaInicio),
        -- Regla 10: el monto del servicio no puede ser negativo.
        CONSTRAINT CK_Servicios_Total CHECK (Total >= 0)
    );
    CREATE INDEX IX_Servicios_IdVehiculo  ON dbo.Servicios (IdVehiculo);
    CREATE INDEX IX_Servicios_Estado      ON dbo.Servicios (Estado);
    CREATE INDEX IX_Servicios_FechaInicio ON dbo.Servicios (FechaInicio);
    CREATE INDEX IX_Servicios_TipoServicio ON dbo.Servicios (TipoServicio);
END
GO

/* ------------------------------------------------------------------------------------------
   2.5) DETALLE_SERVICIO (relación N:M entre Servicios y Trabajadores — Regla 6)
   La Regla 7 (máximo 2 servicios activos por trabajador) se aplica en la capa de negocio
   con transacción y bloqueo (UPDLOCK/HOLDLOCK); MySQL/SQL Server no pueden expresar esa
   regla con una restricción declarativa de tabla.
   ------------------------------------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.DetalleServicio', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DetalleServicio
    (
        IdDetalleServicio INT IDENTITY(1,1) NOT NULL,
        IdServicio        INT           NOT NULL,
        IdTrabajador      INT           NOT NULL,
        FechaAsignacion   DATETIME2(0)  NOT NULL CONSTRAINT DF_DetalleServicio_Fecha DEFAULT (SYSUTCDATETIME()),
        Estado            NVARCHAR(20)  NOT NULL CONSTRAINT DF_DetalleServicio_Estado DEFAULT ('Activo'),
        Observaciones     NVARCHAR(200) NULL,

        CONSTRAINT PK_DetalleServicio PRIMARY KEY (IdDetalleServicio),
        CONSTRAINT FK_DetalleServicio_Servicios FOREIGN KEY (IdServicio)
            REFERENCES dbo.Servicios (IdServicio),
        CONSTRAINT FK_DetalleServicio_Trabajadores FOREIGN KEY (IdTrabajador)
            REFERENCES dbo.Trabajadores (IdTrabajador),
        -- Regla 6: la pareja servicio–trabajador no se puede repetir.
        CONSTRAINT UQ_DetalleServicio_Servicio_Trabajador UNIQUE (IdServicio, IdTrabajador),
        CONSTRAINT CK_DetalleServicio_Estado CHECK (Estado IN ('Activo', 'Retirado'))
    );
    CREATE INDEX IX_DetalleServicio_IdTrabajador ON dbo.DetalleServicio (IdTrabajador);
END
GO

/* ------------------------------------------------------------------------------------------
   2.6) PRODUCTOS (entidad REPUESTO del MER — Regla 10)
   ------------------------------------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.Productos', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Productos
    (
        IdProducto     INT IDENTITY(1,1) NOT NULL,
        Nombre         NVARCHAR(100)  NOT NULL,
        Descripcion    NVARCHAR(300)  NOT NULL,
        PrecioUnitario DECIMAL(12,2)  NOT NULL,
        Stock          DECIMAL(12,3)  NOT NULL,
        Activo         BIT            NOT NULL CONSTRAINT DF_Productos_Activo        DEFAULT (1),
        FechaRegistro  DATETIME2(0)   NOT NULL CONSTRAINT DF_Productos_FechaRegistro DEFAULT (SYSUTCDATETIME()),

        CONSTRAINT PK_Productos PRIMARY KEY (IdProducto),
        -- Regla 10: precios y existencias no negativos.
        CONSTRAINT CK_Productos_PrecioUnitario CHECK (PrecioUnitario >= 0),
        CONSTRAINT CK_Productos_Stock          CHECK (Stock >= 0)
    );
    CREATE INDEX IX_Productos_Nombre ON dbo.Productos (Nombre);
END
GO

/* ------------------------------------------------------------------------------------------
   2.7) PRODUCTOS_UTILIZADOS (relación N:M entre Servicios y Productos — Reglas 9 y 10)
   ------------------------------------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.ProductosUtilizados', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProductosUtilizados
    (
        IdProductoUtilizado INT IDENTITY(1,1) NOT NULL,
        IdServicio          INT           NOT NULL,
        IdProducto          INT           NOT NULL,
        Cantidad            DECIMAL(12,3) NOT NULL,

        CONSTRAINT PK_ProductosUtilizados PRIMARY KEY (IdProductoUtilizado),
        CONSTRAINT FK_ProductosUtilizados_Servicios FOREIGN KEY (IdServicio)
            REFERENCES dbo.Servicios (IdServicio),
        CONSTRAINT FK_ProductosUtilizados_Productos FOREIGN KEY (IdProducto)
            REFERENCES dbo.Productos (IdProducto),
        -- Regla 9: la pareja producto–servicio no se repite.
        CONSTRAINT UQ_ProductosUtilizados_Servicio_Producto UNIQUE (IdServicio, IdProducto),
        -- Regla 10: la cantidad utilizada debe ser mayor que cero.
        CONSTRAINT CK_ProductosUtilizados_Cantidad CHECK (Cantidad > 0)
    );
    CREATE INDEX IX_ProductosUtilizados_IdProducto ON dbo.ProductosUtilizados (IdProducto);
END
GO

/* ------------------------------------------------------------------------------------------
   2.8) FACTURAS (relación GENERA con Clientes — Reglas 12 y 14)
   ------------------------------------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.Facturas', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Facturas
    (
        IdFactura     INT IDENTITY(1,1) NOT NULL,
        NumeroFactura NVARCHAR(20)  NOT NULL,
        IdCliente     INT           NOT NULL,
        FechaFactura  DATETIME2(0)  NOT NULL CONSTRAINT DF_Facturas_Fecha DEFAULT (SYSUTCDATETIME()),
        Subtotal      DECIMAL(12,2) NOT NULL,
        IVA           DECIMAL(12,2) NOT NULL,
        Total         DECIMAL(12,2) NOT NULL,
        MetodoPago    NVARCHAR(30)  NOT NULL,
        Estado        NVARCHAR(20)  NOT NULL CONSTRAINT DF_Facturas_Estado DEFAULT ('Emitida'),
        Notas         NVARCHAR(200) NULL,

        CONSTRAINT PK_Facturas PRIMARY KEY (IdFactura),
        CONSTRAINT UQ_Facturas_NumeroFactura UNIQUE (NumeroFactura),
        -- Regla 12: la factura corresponde a un único cliente.
        CONSTRAINT FK_Facturas_Clientes FOREIGN KEY (IdCliente)
            REFERENCES dbo.Clientes (IdCliente),
        CONSTRAINT CK_Facturas_MetodoPago CHECK (MetodoPago IN ('Efectivo', 'Tarjeta', 'Transferencia')),
        CONSTRAINT CK_Facturas_Estado     CHECK (Estado IN ('Emitida', 'Anulada')),
        -- Reglas 10 y 13: importes no negativos y Total = Subtotal + IVA.
        CONSTRAINT CK_Facturas_Importes CHECK
            (Subtotal >= 0 AND IVA >= 0 AND Total >= 0 AND Total = Subtotal + IVA)
    );
    CREATE INDEX IX_Facturas_IdCliente    ON dbo.Facturas (IdCliente);
    CREATE INDEX IX_Facturas_FechaFactura ON dbo.Facturas (FechaFactura);
END
GO

/* ------------------------------------------------------------------------------------------
   2.9) DETALLE_FACTURA (relación N:M entre Facturas y Servicios — Regla 11)
   ------------------------------------------------------------------------------------------ */
IF OBJECT_ID(N'dbo.DetalleFactura', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DetalleFactura
    (
        IdDetalleFactura INT IDENTITY(1,1) NOT NULL,
        IdFactura        INT           NOT NULL,
        IdServicio       INT           NOT NULL,
        Subtotal         DECIMAL(12,2) NOT NULL,

        CONSTRAINT PK_DetalleFactura PRIMARY KEY (IdDetalleFactura),
        CONSTRAINT FK_DetalleFactura_Facturas FOREIGN KEY (IdFactura)
            REFERENCES dbo.Facturas (IdFactura),
        CONSTRAINT FK_DetalleFactura_Servicios FOREIGN KEY (IdServicio)
            REFERENCES dbo.Servicios (IdServicio),
        -- Regla 11: la pareja factura–servicio no se puede repetir.
        CONSTRAINT UQ_DetalleFactura_Factura_Servicio UNIQUE (IdFactura, IdServicio),
        CONSTRAINT CK_DetalleFactura_Subtotal CHECK (Subtotal >= 0)
    );
    CREATE INDEX IX_DetalleFactura_IdServicio ON dbo.DetalleFactura (IdServicio);
END
GO


/* ------------------------------------------------------------------------------------------
   3) CATÁLOGO DE ÁREAS DE TRABAJO (valores del documento original)
   ------------------------------------------------------------------------------------------ */
IF NOT EXISTS (SELECT 1 FROM dbo.Areas WHERE Nombre = N'Mantenimiento mecánico')
    INSERT INTO dbo.Areas (Nombre) VALUES (N'Mantenimiento mecánico');
IF NOT EXISTS (SELECT 1 FROM dbo.Areas WHERE Nombre = N'Pintura')
    INSERT INTO dbo.Areas (Nombre) VALUES (N'Pintura');
GO


/* ------------------------------------------------------------------------------------------
   4) ANULACIÓN CONTROLADA DE FACTURAS (Regla 14)
   No se elimina ningún registro: la factura pasa a estado 'Anulada' y el motivo se conserva
   en Notas. Los servicios de la factura anulada quedan disponibles para refacturarse.
   Devuelve el número de filas afectadas (0 si no existe o ya estaba anulada).
   ------------------------------------------------------------------------------------------ */
CREATE OR ALTER PROCEDURE dbo.usp_AnularFactura
    @IdFactura INT,
    @Motivo    NVARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT OFF;

    IF NOT EXISTS (SELECT 1 FROM dbo.Facturas WHERE IdFactura = @IdFactura)
        RETURN 0;

    IF EXISTS (SELECT 1 FROM dbo.Facturas WHERE IdFactura = @IdFactura AND Estado = 'Anulada')
        RETURN 0;

    UPDATE dbo.Facturas
       SET Estado = 'Anulada',
           Notas  = LEFT(
                        CASE
                            WHEN @Motivo IS NULL OR LTRIM(RTRIM(@Motivo)) = '' THEN ISNULL(Notas, '')
                            ELSE N'Anulada: ' + LTRIM(RTRIM(@Motivo))
                        END, 200)
     WHERE IdFactura = @IdFactura
       AND Estado = 'Emitida';

    RETURN @@ROWCOUNT;
END
GO


/* ==========================================================================================
   5) CONSULTAS DE VERIFICACIÓN
   Ejecute este bloque por separado (o consulte la salida) para comprobar que el esquema y
   las reglas quedaron correctamente definidos. No modifican datos.
   ========================================================================================== */

-- 5.1) Tablas creadas con su número de columnas
SELECT t.name AS Tabla, COUNT(c.column_id) AS Columnas
FROM sys.tables t
JOIN sys.columns c ON c.object_id = t.object_id
WHERE t.is_ms_shipped = 0
GROUP BY t.name
ORDER BY t.name;
GO

-- 5.2) Integridad referencial declarada (reglas 2, 3, 5, 6, 9, 11 y 12)
SELECT fk.name AS ClaveForanea, OBJECT_NAME(fk.parent_object_id) AS TablaHija,
       OBJECT_NAME(fk.referenced_object_id) AS TablaPadre
FROM sys.foreign_keys fk
ORDER BY TablaHija, ClaveForanea;
GO

-- 5.3) Restricciones CHECK (reglas 1, 4, 8, 10 y 13)
SELECT OBJECT_NAME(parent_object_id) AS Tabla, name AS RestriccionCheck
FROM sys.check_constraints
ORDER BY Tabla, RestriccionCheck;
GO

-- 5.4) Índices únicos (reglas 1, 6, 9 y 11)
SELECT t.name AS Tabla, i.name AS Indice
FROM sys.indexes i
JOIN sys.tables t ON t.object_id = i.object_id
WHERE i.is_unique = 1 AND i.name IS NOT NULL AND t.is_ms_shipped = 0
ORDER BY Tabla, Indice;
GO

-- 5.5) Regla 7: trabajadores que NO cumplen el máximo de 2 servicios activos
--      (debe devolver 0 filas)
SELECT d.IdTrabajador, COUNT(*) AS ServiciosActivos
FROM dbo.DetalleServicio d
WHERE d.Estado = 'Activo'
GROUP BY d.IdTrabajador
HAVING COUNT(*) > 2;
GO

-- 5.6) Regla 13: facturas cuyo total no coincide con Subtotal + IVA (debe devolver 0 filas)
SELECT IdFactura, NumeroFactura, Subtotal, IVA, Total
FROM dbo.Facturas
WHERE Total <> Subtotal + IVA;
GO

-- 5.7) Regla 5: trabajadores asignados a servicios incompatibles con su área (0 filas)
SELECT d.IdDetalleServicio, tr.Nombre, ar.Nombre AS Area,
       s.IdServicio, s.TipoServicio
FROM dbo.DetalleServicio d
JOIN dbo.Trabajadores tr ON tr.IdTrabajador = d.IdTrabajador
JOIN dbo.Areas ar        ON ar.IdArea = tr.IdArea
JOIN dbo.Servicios s     ON s.IdServicio = d.IdServicio
WHERE (s.TipoServicio = 'Mantenimiento' AND ar.Nombre <> N'Mantenimiento mecánico')
   OR (s.TipoServicio = 'Pintura'        AND ar.Nombre <> N'Pintura');
GO
