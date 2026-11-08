
USE TallerMecanicoSantoDomingo;
GO

SET IDENTITY_INSERT Cliente ON;

INSERT INTO Cliente
(IdCliente, Nombre, Apellido, Telefono, Direccion, RUC, Activo, FechaRegistro, Cedula)
VALUES
(1, N'Juan Carlos', N'Pérez López', N'8888-1111',
 N'Colonia Centroamérica, Managua', N'0011206900001A',
 1, '2026-01-10T09:00:00', N'001-120690-0001A'),

(2, N'María Fernanda', N'Sánchez Ruiz', N'8777-2222',
 N'Barrio Monseñor Lezcano, Managua', N'0010511920002B',
 1, '2026-02-05T10:30:00', N'001-051192-0002B'),

(3, N'Carlos Alberto', N'Ramírez Gómez', N'8666-3333',
 N'Residencial Las Colinas, Managua', N'0012308880003C',
 1, '2026-03-18T15:45:00', N'001-230888-0003C');

SET IDENTITY_INSERT Cliente OFF;


SET IDENTITY_INSERT Vehiculo ON;

INSERT INTO Vehiculo
(IdVehiculo, IdCliente, Placa, Marca, Modelo, Anio,
 Activo, FechaRegistro, Kilometraje, Color, NumChasis, Tipo)
VALUES
(1, 1, N'M 123456', N'Toyota', N'Corolla', 2018,
 1, '2026-01-10T09:05:00', 85000, N'Rojo',
 N'JTDBR32E180100001', N'Sedán'),

(2, 1, N'M 234567', N'Nissan', N'Sentra', 2020,
 1, '2026-02-12T11:00:00', 62000, N'Gris',
 N'3N1AB7AP0LY100002', N'Sedán'),

(3, 2, N'M 345678', N'Hyundai', N'Tucson', 2019,
 1, '2026-02-06T10:40:00', 71000, N'Blanco',
 N'KM8J3CA46KU100003', N'Camioneta'),

(4, 3, N'M 456789', N'Mazda', N'CX-5', 2021,
 1, '2026-03-19T16:00:00', 48000, N'Azul',
 N'JM3KFBCM1M0100004', N'Camioneta');

SET IDENTITY_INSERT Vehiculo OFF;


SET IDENTITY_INSERT Trabajador ON;

INSERT INTO Trabajador
(IdTrabajador, Nombre, Apellido, Telefono, Area,
 Estado, FechaIngreso, Correo, FechaSalida)
VALUES
(1, N'Pedro', N'Martínez Rivas', N'8555-4444',
 N'Mantenimiento mecánico', 1, '2026-01-11T08:00:00',
 N'pedro@taller.local', NULL),

(2, N'Luis', N'Hernández Castro', N'8444-5555',
 N'Mantenimiento mecánico', 1, '2026-01-11T08:05:00',
 N'luis@taller.local', NULL),

(3, N'Ana', N'González Mejía', N'8333-6666',
 N'Pintura', 1, '2026-01-11T08:10:00',
 N'ana@taller.local', NULL),

(4, N'Karla', N'Díaz Lara', N'8222-7777',
 N'Pintura', 1, '2026-01-11T08:15:00',
 N'karla@taller.local', NULL);

SET IDENTITY_INSERT Trabajador OFF;


SET IDENTITY_INSERT Servicio ON;

INSERT INTO Servicio
(IdServicio, IdVehiculo, Tipo, Descripcion, DescripcionFalla,
 TipoPintura, Color, Estado, FechaInicio, FechaFin,
 Costo, FechaRegistro)
VALUES
(1, 1, N'Mantenimiento',
 N'Cambio de aceite y revisión general',
 N'Ruido en el motor al acelerar',
 NULL, NULL, N'Abierto',
 '2026-09-20', NULL, 4800.00, '2026-09-20T08:30:00'),

(2, 1, N'Pintura',
 N'Pintura completa por restauración',
 NULL, N'Base solvente', N'Rojo perla', N'Finalizado',
 '2026-08-11', '2026-08-15', 8500.00, '2026-08-11T09:00:00'),

(3, 2, N'Mantenimiento',
 N'Cambio de pastillas de freno',
 N'Chirrido al frenar',
 NULL, NULL, N'Finalizado',
 '2026-08-03', '2026-08-05', 5000.00, '2026-08-03T10:15:00'),

(4, 3, N'Mantenimiento',
 N'Cambio de aceite y afinado',
 N'Consumo elevado de combustible',
 NULL, NULL, N'Abierto',
 '2026-10-10', NULL, 3200.00, '2026-10-10T11:20:00'),

(5, 4, N'Pintura',
 N'Repintura de puerta delantera',
 NULL, N'Base agua', N'Azul metálico', N'Abierto',
 '2026-10-01', NULL, 12500.00, '2026-10-01T13:40:00'),

(6, 2, N'Mantenimiento',
 N'Cambio de bomba de agua',
 N'Fuga de refrigerante',
 NULL, NULL, N'Cancelado',
 '2026-09-05', NULL, 2100.00, '2026-09-05T11:20:00'),

(7, 4, N'Pintura',
 N'Repintura de puerta trasera',
 NULL, N'Base agua', N'Azul', N'Finalizado',
 '2026-09-12', '2026-09-16', 6200.00, '2026-09-12T14:00:00'),

(8, 3, N'Mantenimiento',
 N'Cambio de embrague',
 N'Patina al acelerar',
 NULL, NULL, N'Finalizado',
 '2026-08-20', '2026-08-22', 4300.00, '2026-08-20T09:45:00'),

(9, 2, N'Pintura',
 N'Pintura de parachoques trasero',
 NULL, N'Base agua', N'Gris plata', N'Abierto',
 '2026-10-12', NULL, 3800.00, '2026-10-12T11:20:00'),

(10, 2, N'Mantenimiento',
 N'Alineación y balanceo',
 N'Vibración al conducir',
 NULL, NULL, N'Abierto',
 '2026-10-14', NULL, 2500.00, '2026-10-14T16:10:00');

SET IDENTITY_INSERT Servicio OFF;


INSERT INTO Detalle_Servicio
(IdServicio, IdTrabajador, FechaAsignacion, Estado, Observaciones)
VALUES
(1, 1, '2026-09-20T08:35:00', N'Activo',
 N'Encargado principal del mantenimiento'),
(1, 2, '2026-09-20T08:36:00', N'Activo',
 N'Apoyo en la revisión general'),
(2, 4, '2026-08-11T09:05:00', N'Retirado',
 N'Trabajo de pintura finalizado'),
(3, 2, '2026-08-03T10:20:00', N'Retirado', NULL),
(4, 1, '2026-10-10T11:25:00', N'Activo', NULL),
(5, 3, '2026-10-01T13:45:00', N'Activo',
 N'Pintura en curso'),
(6, 2, '2026-09-05T11:25:00', N'Retirado',
 N'Servicio cancelado por el cliente'),
(7, 4, '2026-09-12T14:05:00', N'Retirado', NULL),
(8, 1, '2026-08-20T09:50:00', N'Retirado', NULL),
(9, 3, '2026-10-12T11:25:00', N'Activo', NULL),
(10, 2, '2026-10-14T16:15:00', N'Activo', NULL);


SET IDENTITY_INSERT Producto ON;

INSERT INTO Producto
(IdProducto, Nombre, Descripcion, Precio, Stock,
 Activo, FechaRegistro, Costo)
VALUES
(1, N'Filtro de aceite',
 N'Filtro para motores a gasolina',
 350.00, 24.000, 1, '2026-01-15T08:00:00', 240.00),

(2, N'Aceite sintético 5W-30 (litro)',
 N'Aceite de motor sintético por litro',
 480.00, 40.000, 1, '2026-01-15T08:05:00', 320.00),

(3, N'Pastillas de freno delanteras',
 N'Juego de pastillas de freno',
 1200.00, 12.000, 1, '2026-01-15T08:10:00', 850.00),

(4, N'Pintura automotriz (galón)',
 N'Pintura para carrocería',
 2500.00, 8.000, 1, '2026-01-15T08:15:00', 1800.00),

(5, N'Barniz transparente (litro)',
 N'Barniz de acabado automotriz',
 900.00, 15.000, 1, '2026-01-15T08:20:00', 600.00),

(6, N'Kit de embrague',
 N'Kit de embrague para vehículo liviano',
 3900.00, 6.000, 1, '2026-01-15T08:25:00', 2800.00);

SET IDENTITY_INSERT Producto OFF;


INSERT INTO Productos_Utilizados
(IdServicio, IdProducto, Cantidad)
VALUES
(1, 1, 1.000),
(1, 2, 4.000),
(2, 4, 2.000),
(2, 5, 2.000),
(3, 3, 1.000),
(4, 1, 1.000),
(4, 2, 4.000),
(5, 4, 1.000),
(5, 5, 1.000),
(7, 4, 1.000),
(7, 5, 1.000),
(8, 6, 1.000),
(9, 4, 1.000),
(9, 5, 1.000);


SET IDENTITY_INSERT Factura ON;

INSERT INTO Factura
(IdFactura, NumeroFactura, IdCliente, RUC, Fecha,
 Subtotal, IVA, Total, MetodoPago, Estado, Notas)
VALUES
(1, N'FAC-2026-0001', 1, N'0011206900001A',
 '2026-08-16T10:00:00',
 13500.00, 2025.00, 15525.00,
 N'Efectivo', N'Emitida', NULL),

(2, N'FAC-2026-0002', 3, N'0012308880003C',
 '2026-09-17T15:30:00',
 6200.00, 930.00, 7130.00,
 N'Tarjeta', N'Emitida', N'Pago con tarjeta'),

(3, N'FAC-2026-0003', 2, N'0010511920002B',
 '2026-08-23T11:00:00',
 4300.00, 645.00, 4945.00,
 N'Transferencia', N'Anulada',
 N'Anulada por corrección de datos');

SET IDENTITY_INSERT Factura OFF;


INSERT INTO Detalle_Factura
(IdFactura, IdServicio, Subtotal)
VALUES
(1, 2, 8500.00),
(1, 3, 5000.00),
(2, 7, 6200.00),
(3, 8, 4300.00);
GO
