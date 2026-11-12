# Análisis de la base de datos del taller

El análisis corresponde a los scripts `TallerMecanicoSantoDomingo.sql` y
`DatosDemostracion.sql`, sobre una base recién creada. Hay **10 tablas: 9 de
negocio y 1 de acceso**, con **59 registros de demostración**. Las cantidades
pueden cambiar si se utiliza la aplicación.

| Tabla | Registros insertados | Clave primaria | Función y relaciones |
|---|---:|---|---|
| Cliente | 3 | IdCliente | Propietarios de vehículos y destinatarios de facturas |
| Vehiculo | 4 | IdVehiculo | Cada vehículo pertenece a un Cliente mediante IdCliente |
| Trabajador | 4 | IdTrabajador | Personal de mantenimiento mecánico o pintura |
| Servicio | 10 | IdServicio | Cada servicio corresponde a un Vehiculo mediante IdVehiculo |
| Detalle_Servicio | 11 | IdServicio, IdTrabajador | Relaciona servicios con sus trabajadores y conserva las asignaciones retiradas |
| Producto | 6 | IdProducto | Catálogo con precio de venta, costo de compra y stock |
| Productos_Utilizados | 14 | IdProducto, IdServicio | Relaciona productos con servicios y registra la cantidad utilizada |
| Factura | 3 | IdFactura | Cada factura pertenece a un Cliente mediante IdCliente |
| Detalle_Factura | 4 | IdServicio, IdFactura | Relaciona servicios con facturas y conserva el subtotal de cada línea |
| Usuarios | 0 | Id | Cuentas de acceso, independientes de los trabajadores |

Un cliente puede tener varios vehículos y facturas; un vehículo puede tener
varios servicios. Las tres tablas con claves compuestas representan relaciones
de muchos a muchos: servicios–trabajadores, servicios–productos y
servicios–facturas. La pareja de identificadores no puede repetirse en una misma
tabla intermedia. Un servicio sí puede aparecer en facturas distintas; la
aplicación comprueba que no tenga ya otra factura emitida.

Los identificadores de las seis tablas principales de negocio son `IDENTITY`.
Los datos de ejemplo usan `IDENTITY_INSERT` para fijarlos y establecer sus
relaciones. `Usuarios.Id` es texto y no se genera con `IDENTITY`.

## Correspondencia entre los diagramas

~~~mermaid
erDiagram
    Cliente ||..o{ Vehiculo : posee
    Vehiculo ||..o{ Servicio : recibe
    Trabajador ||--o{ Detalle_Servicio : realiza
    Servicio ||--o{ Detalle_Servicio : tiene
    Servicio ||--o{ Productos_Utilizados : tiene
    Productos_Utilizados }o--|| Producto : pertenece
    Cliente ||..o{ Factura : recibe
    Factura ||--o{ Detalle_Factura : genera
    Detalle_Factura }o--|| Servicio : corresponde
    Usuarios {
        string Id PK
    }
~~~

El diagrama relacional define las tablas, columnas y claves de SQL Server. En
el entidad–relación, **Posee** corresponde a `Vehiculo.IdCliente` y **Recibe**
a `Servicio.IdVehiculo`. **Realiza** se materializa en `Detalle_Servicio`.
**Tiene** y **Pertenece** enlazan `Productos_Utilizados` con `Servicio` y
`Producto`, respectivamente. **Genera** y **Corresponde** enlazan
`Detalle_Factura` con `Factura` y `Servicio`, respectivamente.

Para sincronizar el dibujo conceptual con el esquema, el atributo `IdRepuesto`
de `Productos_Utilizados` debe llamarse `IdProducto`. También faltan
`Factura.IdFactura` como identificador y la relación de cliente a factura
que existe mediante `Factura.IdCliente`. `Usuarios` aparece en el modelo físico
como tabla de acceso independiente.

Los óvalos dobles de teléfono, correo y color sugieren valores múltiples. El
modelo relacional y los formularios guardan un valor por campo. Si el dibujo
debe describir la implementación actual, esos atributos llevan óvalo simple.

## Qué permiten practicar los datos

- Juan tiene dos vehículos; María y Carlos tienen uno cada uno. Esto permite
  practicar uniones sin suponer que cada cliente aparece una sola vez.
- Hay seis servicios de mantenimiento y cuatro de pintura. Por estado, hay
  cinco abiertos, cuatro finalizados y uno cancelado.
- Las 11 asignaciones se dividen en seis activas y cinco retiradas. Pedro,
  Luis y Ana tienen dos asignaciones activas a servicios abiertos cada uno;
  Karla tiene cero. `Trabajador.Estado` es un `bit`, mientras que
  `Detalle_Servicio.Estado` y `Servicio.Estado` son textos con valores diferentes.
- Los seis productos se han utilizado. Hay 14 líneas de consumo distribuidas
  entre ocho servicios. Los servicios 6 y 10 carecen de productos registrados.
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

## Validaciones del script de creación

Los `CHECK` se aplican en un lote reutilizable tanto al crear la base como al
actualizar una base que ya tiene las tablas del diagrama. `WITH CHECK` valida
las filas existentes al agregar cada restricción. Los cinco índices únicos
filtrados de cédula, RUC, chasis, nombre de usuario y correo normalizados permiten
varios `NULL` y exigen unicidad cuando hay valor. Un cliente particular puede
registrarse únicamente con cédula; el RUC es opcional para los casos de empresa.
`CK_Cliente_Identificacion` exige al menos uno de los dos datos. El lote de
actualización adapta el RUC obligatorio del esquema anterior sin cambiar registros.

Las claves foráneas impiden referencias inexistentes y borrados con registros
dependientes. Otras reglas, como la compatibilidad del área del trabajador, el
máximo de asignaciones activas o la correspondencia entre cliente y servicios
facturados, se comprueban en la aplicación; no las garantiza una clave foránea
por sí sola.

Los **10 ejercicios**, desde selección y filtros hasta subconsultas, CTE y
funciones de ventana, están en [practica.sql](practica.sql), sin soluciones.
