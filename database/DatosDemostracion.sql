/* ==========================================================================================
   TALLER MECÁNICO SANTO DOMINGO — DATOS DE DEMOSTRACIÓN (NO se cargan automáticamente)
   ------------------------------------------------------------------------------------------
   Este script inserta datos FICTICIOS con fines de demostración y de verificación de las
   14 reglas de negocio. NO lo ejecuta la aplicación: debe ejecutarlo usted manualmente
   después de crear el esquema.

       sqlcmd -S localhost -E -C -i "database\TallerMecanicoSantoDomingo.sql"
       sqlcmd -S localhost -E -C -i "database\DatosDemostracion.sql"

   Escenarios que ilustran las reglas (los escenarios de comprobación están al final):
     • Regla 1 : identificadores y placas/RFC únicos.
     • Regla 2 : vehículos MGA-1234 y MGB-5678 pertenecen al mismo cliente (Juan Pérez).
     • Regla 3 : cada servicio pertenece a un solo vehículo.
     • Regla 4 : servicios de tipo Mantenimiento y de tipo Pintura.
     • Regla 5 : los trabajadores se asignan solo a servicios de su propia área.
     • Regla 6 : no existen parejas servicio–trabajador repetidas.
     • Regla 7 : Pedro, Luis y Ana tienen 2 servicios activos (límite); Ana Ruiz 0 (disponible).
     • Regla 8 : todos los servicios finalizados tienen FechaFin >= FechaInicio.
     • Regla 9 : no hay parejas producto–servicio repetidas.
     • Regla 10: cantidades y precios positivos.
     • Regla 11: cada factura tiene al menos un detalle.
     • Regla 12: cada factura agrupa servicios de un mismo cliente.
     • Regla 13 : Total = Subtotal + IVA (IVA del 15 %).
     • Regla 14 : FAC-2026-0003 está ANULADA (no se borró); sus servicios pueden refacturarse.

   ADVERTENCIA: si la base de datos ya contiene clientes, el script se detiene solo para
   evitar duplicados. Para volver a cargarlo, vacíe antes las tablas de datos.
   ========================================================================================== */

USE [TallerMecanicoSantoDomingo];
GO

SET NOCOUNT ON;
GO

IF EXISTS (SELECT 1 FROM dbo.Clientes)
BEGIN
    PRINT N'La base de datos ya contiene datos de clientes. El script de demostración no se ejecutó.';
    RETURN;
END

/* ------------------------------------------------------------------------------------------
   Catálogo de áreas (se crea solo si no existe)
   ------------------------------------------------------------------------------------------ */
IF NOT EXISTS (SELECT 1 FROM dbo.Areas WHERE Nombre = N'Mantenimiento mecánico')
    INSERT INTO dbo.Areas (Nombre) VALUES (N'Mantenimiento mecánico');
IF NOT EXISTS (SELECT 1 FROM dbo.Areas WHERE Nombre = N'Pintura')
    INSERT INTO dbo.Areas (Nombre) VALUES (N'Pintura');

DECLARE @AreaMecanica INT = (SELECT IdArea FROM dbo.Areas WHERE Nombre = N'Mantenimiento mecánico');
DECLARE @AreaPintura  INT = (SELECT IdArea FROM dbo.Areas WHERE Nombre = N'Pintura');

/* ------------------------------------------------------------------------------------------
   CLIENTES
   ------------------------------------------------------------------------------------------ */
SET IDENTITY_INSERT dbo.Clientes ON;
INSERT INTO dbo.Clientes (IdCliente, Nombre, Apellido, Telefono, Direccion, RFC, Activo, FechaRegistro) VALUES
    (1, N'Juan',   N'Pérez López',   N'8888-1111', N'Colonia Centroamérica, Managua',      N'PEPJ850101AB1', 1, '2026-01-10T09:00:00'),
    (2, N'María',  N'Sánchez Ruiz',  N'8777-2222', N'Barrio Monseñor Lezcano, Managua',    N'SAMR900215CD2', 1, '2026-02-05T10:30:00'),
    (3, N'Carlos', N'Ramírez Gómez', N'8666-3333', N'Residencial Las Colinas, Managua',    N'RAGC880520EF3', 1, '2026-03-18T15:45:00');
SET IDENTITY_INSERT dbo.Clientes OFF;

/* ------------------------------------------------------------------------------------------
   VEHÍCULOS (Juan Pérez tiene dos; Regla 2)
   ------------------------------------------------------------------------------------------ */
SET IDENTITY_INSERT dbo.Vehiculos ON;
INSERT INTO dbo.Vehiculos (IdVehiculo, IdCliente, Placa, Marca, Modelo, Anio, Activo, FechaRegistro) VALUES
    (1, 1, N'MGA-1234', N'Toyota',  N'Corolla', 2018, 1, '2026-01-10T09:05:00'),
    (2, 1, N'MGB-5678', N'Nissan',  N'Sentra',  2020, 1, '2026-02-12T11:00:00'),
    (3, 2, N'MGC-9012', N'Hyundai', N'Tucson',  2019, 1, '2026-02-06T10:40:00'),
    (4, 3, N'MGD-3456', N'Mazda',   N'CX-5',    2021, 1, '2026-03-19T16:00:00');
SET IDENTITY_INSERT dbo.Vehiculos OFF;

/* ------------------------------------------------------------------------------------------
   TRABAJADORES (dos por área; Regla 5)
   ------------------------------------------------------------------------------------------ */
SET IDENTITY_INSERT dbo.Trabajadores ON;
INSERT INTO dbo.Trabajadores (IdTrabajador, Nombre, Apellido, Telefono, IdArea, Activo, FechaRegistro) VALUES
    (1, N'Pedro', N'Martínez Rivas',   N'8555-4444', @AreaMecanica, 1, '2026-01-11T08:00:00'),
    (2, N'Luis',  N'Hernández Castro', N'8444-5555', @AreaMecanica, 1, '2026-01-11T08:05:00'),
    (3, N'Ana',   N'González Mejía',   N'8333-6666', @AreaPintura,  1, '2026-01-11T08:10:00'),
    (4, N'Karla', N'Díaz Lara',        N'8222-7777', @AreaPintura,  1, '2026-01-11T08:15:00');
SET IDENTITY_INSERT dbo.Trabajadores OFF;

/* ------------------------------------------------------------------------------------------
   SERVICIOS (Mantenimiento y Pintura; Reglas 3, 4 y 8)
   ------------------------------------------------------------------------------------------ */
SET IDENTITY_INSERT dbo.Servicios ON;
INSERT INTO dbo.Servicios (IdServicio, IdVehiculo, TipoServicio, Descripcion, DescripcionFalla, TipoPintura, Color, Estado, FechaInicio, FechaFin, Total, FechaRegistro) VALUES
    (1,  1, N'Mantenimiento', N'Cambio de aceite y revisión general', N'Ruido en el motor al acelerar', NULL, NULL, N'Abierto',    '2026-09-20', NULL,         4800.00, '2026-09-20T08:30:00'),
    (2,  1, N'Pintura',       N'Pintura completa por restauración',   NULL, N'Base solvente', N'Rojo perla',  N'Finalizado', '2026-08-11', '2026-08-15', 8500.00, '2026-08-11T09:00:00'),
    (3,  2, N'Mantenimiento', N'Cambio de pastillas de freno',        N'Chirrido al frenar',            NULL, NULL, N'Finalizado', '2026-08-03', '2026-08-05', 5000.00, '2026-08-03T10:15:00'),
    (4,  3, N'Mantenimiento', N'Cambio de aceite y afinado',          N'Consumo elevado de combustible', NULL, NULL, N'Abierto',   '2026-10-10', NULL,         3200.00, '2026-10-10T11:20:00'),
    (5,  4, N'Pintura',       N'Repintura de puerta delantera',       NULL, N'Base agua', N'Azul metálico', N'Abierto', '2026-10-01', NULL, 12500.00, '2026-10-01T13:40:00'),
    (6,  2, N'Mantenimiento', N'Cambio de bomba de agua',             N'Fuga de refrigerante',          NULL, NULL, N'Cancelado',  '2026-09-05', NULL,         2100.00, '2026-09-05T11:20:00'),
    (7,  4, N'Pintura',       N'Repintura de puerta delantera',       NULL, N'Base agua',     N'Azul',        N'Finalizado', '2026-09-12', '2026-09-16', 6200.00, '2026-09-12T14:00:00'),
    (8,  3, N'Mantenimiento', N'Cambio de embrague',                  N'Patina al acelerar',            NULL, NULL, N'Finalizado', '2026-08-20', '2026-08-22', 4300.00, '2026-08-20T09:45:00'),
    (9,  2, N'Pintura',       N'Pintura de parachoques trasero',      NULL, N'Base agua', N'Gris plata',  N'Abierto',    '2026-10-12', NULL,         3800.00, '2026-10-12T11:20:00'),
    (10, 2, N'Mantenimiento', N'Alineación y balanceo',               NULL, NULL, NULL, N'Abierto', '2026-10-14', NULL, 2500.00, '2026-10-14T16:10:00');
SET IDENTITY_INSERT dbo.Servicios OFF;

/* ------------------------------------------------------------------------------------------
   ASIGNACIONES (Reglas 5, 6 y 7)
   Trabajadores con 2 servicios activos: Pedro (1,4), Luis (1,10) y Ana (5,9).
   Karla queda con 0 servicios activos (disponible).
   ------------------------------------------------------------------------------------------ */
SET IDENTITY_INSERT dbo.DetalleServicio ON;
INSERT INTO dbo.DetalleServicio (IdDetalleServicio, IdServicio, IdTrabajador, FechaAsignacion, Estado, Observaciones) VALUES
    (1,  1,  1, '2026-09-20T08:35:00', N'Activo',   N'Encargado principal del mantenimiento'),
    (2,  1,  2, '2026-09-20T08:36:00', N'Activo',   N'Apoyo en la revisión general'),
    (3,  2,  4, '2026-08-11T09:05:00', N'Retirado', N'Trabajo de pintura finalizado'),
    (4,  3,  2, '2026-08-03T10:20:00', N'Retirado', NULL),
    (5,  4,  1, '2026-10-10T08:40:00', N'Activo',   NULL),
    (6,  5,  3, '2026-10-01T09:10:00', N'Activo',   N'Pintura en curso'),
    (7,  6,  2, '2026-09-05T09:00:00', N'Retirado', N'Servicio cancelado por el cliente'),
    (8,  7,  4, '2026-09-12T14:05:00', N'Retirado', NULL),
    (9,  8,  1, '2026-08-20T09:50:00', N'Retirado', NULL),
    (10, 9,  3, '2026-10-12T11:25:00', N'Activo',   NULL),
    (11, 10, 2, '2026-10-14T16:15:00', N'Activo',   NULL);
SET IDENTITY_INSERT dbo.DetalleServicio OFF;

/* ------------------------------------------------------------------------------------------
   PRODUCTOS (REPUESTOS; Regla 10)
   ------------------------------------------------------------------------------------------ */
SET IDENTITY_INSERT dbo.Productos ON;
INSERT INTO dbo.Productos (IdProducto, Nombre, Descripcion, PrecioUnitario, Stock, Activo, FechaRegistro) VALUES
    (1, N'Filtro de aceite 10W-40',        N'Filtro de aceite para motor de gasolina', 350.00,  24.000, 1, '2026-01-15T08:00:00'),
    (2, N'Aceite sintético 5W-30 (litro)', N'Aceite de motor sintético',               480.00,  40.000, 1, '2026-01-15T08:05:00'),
    (3, N'Pastillas de freno delanteras',  N'Juego de pastillas cerámicas',            1200.00, 12.000, 1, '2026-01-15T08:10:00'),
    (4, N'Pintura base solvente (galón)',  N'Pintura automotriz base solvente',        2500.00,  8.000, 1, '2026-01-15T08:15:00'),
    (5, N'Barniz transparente (litro)',    N'Barniz de acabado automotriz',             900.00, 15.000, 1, '2026-01-15T08:20:00');
SET IDENTITY_INSERT dbo.Productos OFF;

/* ------------------------------------------------------------------------------------------
   PRODUCTOS UTILIZADOS (Reglas 9 y 10)
   ------------------------------------------------------------------------------------------ */
SET IDENTITY_INSERT dbo.ProductosUtilizados ON;
INSERT INTO dbo.ProductosUtilizados (IdProductoUtilizado, IdServicio, IdProducto, Cantidad) VALUES
    (1, 1,  1, 1.000),
    (2, 1,  2, 4.000),
    (3, 3,  2, 5.000),
    (4, 3,  3, 1.000),
    (5, 5,  4, 2.000),
    (6, 7,  4, 1.000),
    (7, 7,  5, 2.000),
    (8, 9,  4, 1.000),
    (9, 9,  5, 1.000),
    (10, 10, 1, 1.000);
SET IDENTITY_INSERT dbo.ProductosUtilizados OFF;

/* ------------------------------------------------------------------------------------------
   FACTURAS (Reglas 11, 12, 13 y 14)
   IVA del 15 % sobre el subtotal. Total = Subtotal + IVA.
   ------------------------------------------------------------------------------------------ */
SET IDENTITY_INSERT dbo.Facturas ON;
INSERT INTO dbo.Facturas (IdFactura, NumeroFactura, IdCliente, FechaFactura, Subtotal, IVA, Total, MetodoPago, Estado, Notas) VALUES
    (1, N'FAC-2026-0001', 1, '2026-08-16T10:00:00', 13500.00, 2025.00, 15525.00, N'Efectivo',      N'Emitida', NULL),
    (2, N'FAC-2026-0002', 3, '2026-09-17T15:30:00',  6200.00,  930.00,  7130.00, N'Tarjeta',       N'Emitida', N'Pago con tarjeta de crédito'),
    (3, N'FAC-2026-0003', 2, '2026-08-23T11:00:00',  4300.00,  645.00,  4945.00, N'Transferencia', N'Anulada', N'Anulada: corrección de datos del cliente');
SET IDENTITY_INSERT dbo.Facturas OFF;

SET IDENTITY_INSERT dbo.DetalleFactura ON;
INSERT INTO dbo.DetalleFactura (IdDetalleFactura, IdFactura, IdServicio, Subtotal) VALUES
    (1, 1, 2, 8500.00),
    (2, 1, 3, 5000.00),
    (3, 2, 7, 6200.00),
    (4, 3, 8, 4300.00);
SET IDENTITY_INSERT dbo.DetalleFactura OFF;

PRINT N'Datos de demostración insertados correctamente.';
GO


/* ==========================================================================================
   COMPROBACIÓN DE LOS DATOS DE DEMOSTRACIÓN
   ========================================================================================== */

-- Conteo de registros cargados
SELECT 'Clientes' AS Tabla, COUNT(*) AS Registros FROM dbo.Clientes
UNION ALL SELECT 'Vehiculos',          COUNT(*) FROM dbo.Vehiculos
UNION ALL SELECT 'Areas',              COUNT(*) FROM dbo.Areas
UNION ALL SELECT 'Trabajadores',       COUNT(*) FROM dbo.Trabajadores
UNION ALL SELECT 'Servicios',          COUNT(*) FROM dbo.Servicios
UNION ALL SELECT 'DetalleServicio',    COUNT(*) FROM dbo.DetalleServicio
UNION ALL SELECT 'Productos',          COUNT(*) FROM dbo.Productos
UNION ALL SELECT 'ProductosUtilizados',COUNT(*) FROM dbo.ProductosUtilizados
UNION ALL SELECT 'Facturas',           COUNT(*) FROM dbo.Facturas
UNION ALL SELECT 'DetalleFactura',     COUNT(*) FROM dbo.DetalleFactura;
GO

-- Regla 7: carga de servicios activos por trabajador (ninguno debe superar 2)
SELECT tr.IdTrabajador, tr.Nombre + N' ' + tr.Apellido AS Trabajador,
       COUNT(d.IdDetalleServicio) AS ServiciosActivos
FROM dbo.Trabajadores tr
LEFT JOIN dbo.DetalleServicio d ON d.IdTrabajador = tr.IdTrabajador AND d.Estado = 'Activo'
GROUP BY tr.IdTrabajador, tr.Nombre, tr.Apellido
ORDER BY ServiciosActivos DESC;
GO

-- Reglas 12 y 13: factura, cliente, subtotal, IVA y total
SELECT f.NumeroFactura, c.Nombre + N' ' + c.Apellido AS Cliente,
       f.Subtotal, f.IVA, f.Total, f.Estado
FROM dbo.Facturas f
JOIN dbo.Clientes c ON c.IdCliente = f.IdCliente
ORDER BY f.NumeroFactura;
GO
