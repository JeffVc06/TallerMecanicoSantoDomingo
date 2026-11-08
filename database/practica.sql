-- PRÁCTICA: 10 EJERCICIOS DE CONSULTAS EN SQL SERVER
-- Base: TallerMecanicoSantoDomingo.sql + DatosDemostracion.sql.
-- Ordenados de menor a mayor dificultad. Solo se solicitan consultas SELECT.
-- Escribe tu consulta debajo de cada enunciado; no se incluyen soluciones.
-- Los controles de filas suponen una base recién cargada con los datos de ejemplo.
-- No uses IDs fijos para reproducir los resultados: aplica las condiciones pedidas.
-- Consulta AnalisisBaseDatos.md para conocer las relaciones y los datos disponibles.

USE TallerMecanicoSantoDomingo;
GO

/*
1. DIRECTORIO DE CLIENTES — Muy fácil
   Muestra Nombre, Apellido, Telefono y RUC de todos los clientes.
   Ordena por Apellido y, en caso de empate, por Nombre, ambos ascendentemente.
   Tabla: Cliente.
   Practica: SELECT de columnas y ORDER BY.
   Control: 3 filas.
*/



/*
2. PRODUCTOS CON POCO STOCK — Fácil
   Busca los productos activos que tengan Stock menor o igual a 15 y Precio
   mayor o igual a 900. Muestra Nombre, Stock y Precio.
   Ordena por Stock ascendente y, en caso de empate, por Nombre.
   Tabla: Producto.
   Practica: WHERE, AND, comparaciones y ordenamiento.
   Control: 3 filas.
*/



/*
3. SERVICIOS ABIERTOS EN OCTUBRE — Fácil
   Muestra IdServicio, Descripcion, FechaInicio y Costo de los servicios
   con Estado = 'Abierto', iniciados en octubre de 2026 y sin FechaFin.
   Usa un intervalo desde el 1 de octubre inclusive hasta el 1 de noviembre
   exclusivo. Ordena por FechaInicio y luego por IdServicio.
   Tabla: Servicio.
   Practica: filtros de fechas y valores nulos con IS NULL.
   Control: 4 filas.
*/



/*
4. VEHÍCULOS Y PROPIETARIOS — Básico / intermedio
   Muestra Placa, Marca, Modelo y Anio de cada vehículo, junto con el
   nombre completo de su propietario en una columna llamada Propietario.
   Ordena por el apellido del propietario y después por Placa.
   Tablas: Vehiculo y Cliente.
   Practica: INNER JOIN, alias y concatenación de texto.
   Control: 4 filas; un mismo propietario puede aparecer más de una vez.
*/



/*
5. IMPORTE DE LOS PRODUCTOS UTILIZADOS — Intermedio
   Muestra una fila por cada producto utilizado en un servicio, incluyendo:
   IdServicio, descripción del servicio, nombre del producto, Cantidad,
   Precio y una columna Importe calculada como Cantidad * Precio.
   Presenta Importe con dos decimales y ordena por IdServicio e IdProducto.
   Usa el precio actual del producto; este cálculo es una estimación,
   no el total de la factura ni el costo de compra del producto.
   Tablas: Servicio, Productos_Utilizados y Producto.
   Practica: JOIN de tres tablas, operaciones y conversión de decimales.
   Control: 10 filas.
*/



/*
6. RESUMEN DE SERVICIOS POR TIPO Y ESTADO — Intermedio
   Para cada combinación existente de Tipo y Estado, muestra la cantidad
   de servicios, la suma de Costo y el promedio de Costo con dos decimales.
   Incluye los servicios cancelados y ordena por Tipo y Estado.
   Tabla: Servicio.
   Practica: GROUP BY, COUNT, SUM y AVG.
   Control: 5 grupos; sus cantidades deben sumar 10 servicios.
*/



/*
7. TRABAJADORES CON DOS O MÁS SERVICIOS ABIERTOS — Intermedio / avanzado
   Muestra IdTrabajador, nombre completo, Area y cantidad de servicios
   abiertos para los trabajadores activos que tengan al menos dos.
   Cuenta solo asignaciones con Detalle_Servicio.Estado = 'Activo'
   cuyo servicio tenga Servicio.Estado = 'Abierto'.
   Recuerda que Trabajador.Estado es un bit: 1 significa trabajador activo.
   Ordena por cantidad descendente y luego por IdTrabajador.
   Tablas: Trabajador, Detalle_Servicio y Servicio.
   Practica: JOIN, filtros en varias tablas, GROUP BY y HAVING.
   Control: 3 trabajadores.
*/



/*
8. SERVICIOS POR ENCIMA DEL PROMEDIO DE SU TIPO — Avanzado
   Busca los servicios cuyo Costo supere el promedio de los servicios
   de su mismo Tipo. Excluye los cancelados tanto de los candidatos
   como del cálculo del promedio. No compares con el promedio general.
   Muestra IdServicio, Tipo, Descripcion y Costo.
   Resuelve con una subconsulta correlacionada y ordena por Tipo,
   Costo descendente e IdServicio.
   Tabla: Servicio, consultada con distintos alias.
   Practica: subconsulta correlacionada y AVG.
   Control: 5 servicios.
*/



/*
9. SERVICIOS FINALIZADOS SIN FACTURA EMITIDA — Avanzado
   Encuentra los servicios finalizados que no estén incluidos en ninguna
   factura con Estado = 'Emitida'. Una factura anulada no los descarta.
   Muestra IdServicio, Descripcion, Placa, nombre completo del cliente
   y Costo. Ordena por IdServicio.
   Resuelve la ausencia de facturación con NOT EXISTS; no basta buscar
   servicios sin filas en Detalle_Factura porque se conserva el historial.
   Tablas: Servicio, Vehiculo, Cliente, Detalle_Factura y Factura.
   Practica: JOIN y NOT EXISTS con una condición dentro de la subconsulta.
   Control: 1 servicio.
*/



/*
10. REPORTE GENERAL Y CLASIFICACIÓN DE CLIENTES — Difícil
    Construye un reporte con una sola fila por cliente y estas columnas:
    IdCliente, nombre completo, cantidad de vehículos, cantidad total de
    servicios, cantidad de servicios abiertos, cantidad de facturas emitidas,
    total facturado con IVA, porcentaje del total facturado por el taller
    y posición según su total facturado, de mayor a menor.

    Incluye todos los clientes aunque alguna cantidad sea cero.
    Cuenta todos los vehículos y servicios asociados, sin filtrar por Activo
    ni excluir servicios cancelados del total de servicios.
    Para contar los abiertos, considera únicamente Estado = 'Abierto'.
    Cuenta y suma únicamente facturas con Estado = 'Emitida', asociadas
    directamente al cliente mediante Factura.IdCliente.
    Muestra ceros en lugar de NULL y el porcentaje con dos decimales;
    si no hay facturación emitida, el porcentaje debe ser cero.
    Usa DENSE_RANK para que los empates compartan posición sin saltos.
    Ordena por posición y luego por IdCliente.

    Agrupa vehículos/servicios y facturas por cliente en CTE independientes
    antes de unirlos: unir todos los detalles directamente puede repetir
    facturas y multiplicar los totales. No uses SUM(DISTINCT Total) para
    ocultar repeticiones; dos facturas distintas pueden tener el mismo total.
    Tablas: Cliente, Vehiculo, Servicio y Factura.
    Practica: CTE, LEFT JOIN, agregación condicional, ISNULL o COALESCE,
    NULLIF, porcentajes y funciones de ventana.
    Control: 3 filas; la suma de TotalFacturado debe ser 22655.00.
*/
