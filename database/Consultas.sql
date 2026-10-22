-- Consultas de lectura: no crean, cambian ni eliminan datos.
USE TallerMecanicoSantoDomingo;
GO

-- Ver los registros de cada tabla.
SELECT * FROM Cliente;
SELECT * FROM Vehiculo;
SELECT * FROM Trabajador;
SELECT * FROM Servicio;
SELECT * FROM Detalle_Servicio;
SELECT * FROM Producto;
SELECT * FROM Productos_Utilizados;
SELECT * FROM Factura;
SELECT * FROM Detalle_Factura;

-- Ver las cuentas de acceso sin mostrar sus datos técnicos de contraseña.
SELECT Id, UserName, Email, LockoutEnabled FROM Usuarios;

-- Vehículos y sus propietarios.
SELECT v.Placa, v.Marca, v.Modelo, c.Nombre, c.Apellido
FROM Vehiculo v
INNER JOIN Cliente c ON v.IdCliente = c.IdCliente;

-- Servicios de cada vehículo.
SELECT s.IdServicio, v.Placa, s.Descripcion, s.Tipo, s.Estado, s.Costo
FROM Servicio s
INNER JOIN Vehiculo v ON s.IdVehiculo = v.IdVehiculo;

-- Trabajadores asignados a cada servicio.
SELECT d.IdServicio, t.Nombre, t.Apellido, t.Area, d.Estado
FROM Detalle_Servicio d
INNER JOIN Trabajador t ON d.IdTrabajador = t.IdTrabajador;

-- Productos utilizados y su cantidad por servicio.
SELECT u.IdServicio, p.Nombre, u.Cantidad, p.Precio
FROM Productos_Utilizados u
INNER JOIN Producto p ON u.IdProducto = p.IdProducto;

-- Facturas con el cliente y los importes calculados al emitirlas.
SELECT f.NumeroFactura, c.Nombre, c.Apellido, f.Subtotal, f.IVA, f.Total, f.Estado
FROM Factura f
INNER JOIN Cliente c ON f.IdCliente = c.IdCliente;

-- Servicios incluidos en cada factura.
SELECT f.NumeroFactura, s.Descripcion, d.Subtotal
FROM Detalle_Factura d
INNER JOIN Factura f ON d.IdFactura = f.IdFactura
INNER JOIN Servicio s ON d.IdServicio = s.IdServicio;

-- Filtrar servicios abiertos.
SELECT IdServicio, Descripcion, FechaInicio, Costo
FROM Servicio
WHERE Estado = 'Abierto';

-- Cuántos servicios activos tiene cada trabajador con asignaciones abiertas.
SELECT t.IdTrabajador, t.Nombre, t.Apellido, COUNT(*) AS ServiciosActivos
FROM Detalle_Servicio d
INNER JOIN Trabajador t ON d.IdTrabajador = t.IdTrabajador
INNER JOIN Servicio s ON d.IdServicio = s.IdServicio
WHERE d.Estado = 'Activo' AND s.Estado = 'Abierto'
GROUP BY t.IdTrabajador, t.Nombre, t.Apellido;

-- Total facturado sin sumar facturas anuladas.
SELECT ISNULL(SUM(Total), 0) AS TotalFacturado
FROM Factura
WHERE Estado = 'Emitida';
