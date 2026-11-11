-- Consultas de lectura: no crean, cambian ni eliminan datos.
USE TallerMecanicoSantoDomingo;
GO

-- Ver los registros de cada tabla.
SELECT * FROM dbo.Cliente;
SELECT * FROM dbo.Vehiculo;
SELECT * FROM dbo.Trabajador;
SELECT * FROM dbo.Servicio;
SELECT * FROM dbo.Detalle_Servicio;
SELECT * FROM dbo.Producto;
SELECT * FROM dbo.Productos_Utilizados;
SELECT * FROM dbo.Factura;
SELECT * FROM dbo.Detalle_Factura;

-- Ver las cuentas de acceso sin mostrar sus datos técnicos de contraseña.
SELECT Id, UserName, Email, LockoutEnabled FROM dbo.Usuarios;

-- Vehículos y sus propietarios.
SELECT v.IdVehiculo, v.Placa, v.Marca, v.Modelo, c.IdCliente, c.Nombre, c.Apellido
FROM dbo.Vehiculo v
INNER JOIN dbo.Cliente c ON v.IdCliente = c.IdCliente;

-- Servicios de cada vehículo.
SELECT s.IdServicio, v.Placa, s.Descripcion, s.Tipo, s.Estado, s.Costo
FROM dbo.Servicio s
INNER JOIN dbo.Vehiculo v ON s.IdVehiculo = v.IdVehiculo;

-- Trabajadores asignados a cada servicio.
SELECT s.IdServicio, s.Descripcion, t.IdTrabajador, t.Nombre, t.Apellido, t.Area, d.Estado AS EstadoAsignacion
FROM dbo.Detalle_Servicio d
INNER JOIN dbo.Servicio s ON s.IdServicio = d.IdServicio
INNER JOIN dbo.Trabajador t ON d.IdTrabajador = t.IdTrabajador;

-- Productos utilizados y su cantidad por servicio.
SELECT s.IdServicio, s.Descripcion, p.IdProducto, p.Nombre, u.Cantidad, p.Precio AS PrecioActual
FROM dbo.Productos_Utilizados u
INNER JOIN dbo.Servicio s ON s.IdServicio = u.IdServicio
INNER JOIN dbo.Producto p ON u.IdProducto = p.IdProducto;

-- Facturas con el cliente y los importes calculados al emitirlas.
SELECT f.NumeroFactura, c.IdCliente, c.Nombre, c.Apellido, f.RUC, f.Subtotal, f.IVA, f.Total, f.Estado
FROM dbo.Factura f
INNER JOIN dbo.Cliente c ON f.IdCliente = c.IdCliente;

-- Servicios incluidos en cada factura.
SELECT f.NumeroFactura, s.IdServicio, s.Descripcion, d.Subtotal
FROM dbo.Detalle_Factura d
INNER JOIN dbo.Factura f ON d.IdFactura = f.IdFactura
INNER JOIN dbo.Servicio s ON d.IdServicio = s.IdServicio;

-- Filtrar servicios abiertos.
SELECT IdServicio, Descripcion, FechaInicio, Costo
FROM dbo.Servicio
WHERE Estado = 'Abierto';

-- Cuántos servicios activos tiene cada trabajador con asignaciones abiertas.
SELECT t.IdTrabajador, t.Nombre, t.Apellido, COUNT(*) AS ServiciosActivos
FROM dbo.Detalle_Servicio d
INNER JOIN dbo.Trabajador t ON d.IdTrabajador = t.IdTrabajador
INNER JOIN dbo.Servicio s ON d.IdServicio = s.IdServicio
WHERE t.Estado = 1 AND d.Estado = 'Activo' AND s.Estado = 'Abierto'
GROUP BY t.IdTrabajador, t.Nombre, t.Apellido;

-- Total facturado sin sumar facturas anuladas.
SELECT ISNULL(SUM(Total), 0) AS TotalFacturado
FROM dbo.Factura
WHERE Estado = 'Emitida';
