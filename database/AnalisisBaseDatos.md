# Análisis de la base de datos del taller

El análisis corresponde a los scripts `TallerMecanicoSantoDomingo.sql` y
`DatosDemostracion.sql`, sobre una base recién creada. Hay **10 tablas: 9 de
negocio y 1 de acceso**, con **54 registros de demostración**. Las cantidades
pueden cambiar si se utiliza la aplicación.

| Tabla | Registros insertados | Clave primaria | Función y relaciones |
|---|---:|---|---|
| Cliente | 3 | IdCliente | Propietarios de vehículos y destinatarios de facturas |
| Vehiculo | 4 | IdVehiculo | Cada vehículo pertenece a un Cliente mediante IdCliente |
| Trabajador | 4 | IdTrabajador | Personal de mantenimiento mecánico o pintura |
| Servicio | 10 | IdServicio | Cada servicio corresponde a un Vehiculo mediante IdVehiculo |
| Detalle_Servicio | 11 | IdServicio, IdTrabajador | Relaciona servicios con sus trabajadores y conserva las asignaciones retiradas |
| Producto | 5 | IdProducto | Catálogo con precio de venta, costo de compra y stock |
| Productos_Utilizados | 10 | IdProducto, IdServicio | Relaciona productos con servicios y registra la cantidad utilizada |
| Factura | 3 | IdFactura | Cada factura pertenece a un Cliente mediante IdCliente |
| Detalle_Factura | 4 | IdServicio, IdFactura | Relaciona servicios con facturas y conserva el subtotal de cada línea |
| Usuarios | 0 | Id | Cuentas de acceso, independientes de los trabajadores |

Un cliente puede tener varios vehículos y facturas; un vehículo puede tener
varios servicios. Las tres tablas con claves compuestas representan relaciones
de muchos a muchos: servicios–trabajadores, servicios–productos y
servicios–facturas. La pareja de identificadores no puede repetirse en una misma
tabla intermedia. Un servicio sí puede aparecer en facturas distintas; la
aplicación comprueba que no tenga ya otra factura emitida.

Los identificadores de las siete tablas principales de negocio son `IDENTITY`.
Los datos de ejemplo usan `IDENTITY_INSERT` para fijarlos y establecer sus
relaciones. `Usuarios.Id` es texto y no se genera con `IDENTITY`.

## Qué permiten practicar los datos

- Juan tiene dos vehículos; María y Carlos tienen uno cada uno. Esto permite
  practicar uniones sin suponer que cada cliente aparece una sola vez.
- Hay seis servicios de mantenimiento y cuatro de pintura. Por estado, hay
  cinco abiertos, cuatro finalizados y uno cancelado.
- Las 11 asignaciones se dividen en seis activas y cinco retiradas. Pedro,
  Luis y Ana tienen dos asignaciones activas a servicios abiertos cada uno;
  Karla tiene cero. `Trabajador.Estado` es un `bit`, mientras que
  `Detalle_Servicio.Estado` y `Servicio.Estado` son textos con valores diferentes.
- Los cinco productos se han utilizado. Hay diez líneas de consumo distribuidas
  entre seis servicios; otros cuatro servicios no tienen productos registrados.
  Las cantidades admiten decimales, aunque los ejemplos actuales sean enteros.
- Hay dos facturas emitidas y una anulada. Las emitidas suman **22655.00 con
  IVA**: 15525.00 de Juan y 7130.00 de Carlos. María solo tiene una factura
  anulada, por lo que su total emitido es cero.
- La factura 1 incluye dos servicios. Sumar su `Total` después de unirla con
  `Detalle_Factura` lo contaría dos veces. Los reportes deben respetar qué
  representa cada fila antes de agrupar.
- El servicio 8 está finalizado y asociado únicamente a una factura anulada.
  Por eso, buscar servicios sin factura emitida requiere revisar el estado
  de la factura y no solamente la existencia de un detalle.
- No hay ejemplos de clientes sin vehículos, productos sin uso, trabajadores
  inactivos ni cédulas o chasis nulos. El esquema permite esos casos; una
  consulta general debe contemplarlos cuando el enunciado lo requiera.

## Importes y consistencia de los datos

`Servicio.Costo` es el importe del servicio que utiliza la aplicación al
facturar. `Producto.Costo` es el costo de compra y `Producto.Precio` es el precio
de venta actual. `Cantidad * Precio` estima el valor de los productos utilizados;
no representa por sí solo la factura ni debe sumarse automáticamente al importe
del servicio. `Productos_Utilizados` no guarda el precio histórico del producto.

Las tres facturas tienen `Total = Subtotal + IVA`; sus subtotales coinciden con
la suma de sus detalles. Los ejemplos aplican un IVA del 15 %, pero el `CHECK`
solo valida los importes no negativos y la igualdad del total: **no fija una
tasa de IVA**. Los servicios facturados pertenecen al cliente correspondiente.

Hay dos observaciones sobre los datos de ejemplo:

- Las asignaciones de los servicios 4, 5 y 6 tienen una hora anterior a la
  `FechaRegistro` de su servicio en el mismo día. Si la fecha de registro
  representa la creación del servicio, esas horas requieren revisión.
- Los servicios 5, 7 y 9 indican pintura de base agua, pero utilizan el producto
  4, descrito como pintura de base solvente. Es una discrepancia descriptiva
  que las claves foráneas no comprueban.

Estas observaciones se documentan; los datos insertados se conservan.

## Simplificación del script de creación

Las claves primarias simples, referencias, valores predeterminados, restricciones
`UNIQUE`, nulabilidad y `CHECK` de un solo campo se declaran junto al campo.
`IDENTITY` equivale a `IDENTITY(1,1)`. Se conservan los nombres de los `CHECK`
para mantener su correspondencia con el modelo de la aplicación y las pruebas.
Se eliminan los separadores `GO` innecesarios entre tablas.

Permanecen fuera de la línea del campo solamente los elementos que lo necesitan:

- Las tres claves primarias compuestas, porque incluyen dos columnas.
- Los `CHECK` de fechas del trabajador y del servicio y de importes de la factura,
  porque comparan varias columnas. En SQL Server se declaran a nivel de tabla.
- Los cuatro índices únicos filtrados de cédula, chasis, nombre de usuario y
  correo normalizados. Permiten varios `NULL` y exigen unicidad cuando hay valor;
  un `UNIQUE` simple sobre una columna nullable permitiría un solo `NULL`.

Las claves foráneas impiden referencias inexistentes y borrados con registros
dependientes. Otras reglas, como la compatibilidad del área del trabajador, el
máximo de asignaciones activas o la correspondencia entre cliente y servicios
facturados, se comprueban en la aplicación; no las garantiza una clave foránea
por sí sola.

Los **10 ejercicios**, desde selección y filtros hasta subconsultas, CTE y
funciones de ventana, están en [practica.sql](practica.sql), sin soluciones.
