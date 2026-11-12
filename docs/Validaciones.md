# Validaciones del taller sin cambios de esquema

Esta intervención trabaja sobre la base ya terminada. No modifica los archivos
de `database/`, las entidades mapeadas ni `TallerDbContext`. Había cambios locales
previos en esos archivos: se conservaron sus contenidos al comenzar esta tarea.
No se crean ni ejecutan migraciones, tablas, índices, restricciones, procedimientos
o triggers. El inicializador existente ahora solo comprueba tablas y columnas;
se retiró su ejecución automática de DDL. La creación inicial de cuentas sigue
utilizando Identity y escrituras normales en `Usuarios`.

## Correspondencia y reglas

| Concepto | Representación y protección |
|---|---|
| Cliente | `Cliente`; `RFC` en C# corresponde a `RUC`. Cédula y RUC se normalizan y no se repiten cuando tienen valor. |
| Vehículo | `Vehiculo`; cliente existente, placa obligatoria y única y chasis único cuando tenga valor. Un vehículo con servicios no puede cambiar de propietario porque no existe historial de transferencias. |
| Trabajo | `Servicio`; `TipoServicio` se mapea a `Tipo` y `Total` a `Costo`. Mantenimiento requiere falla; pintura requiere tipo de pintura y color. Fechas e importes se validan en crear, editar y finalizar. |
| Personal | `Trabajador.Area` es un atributo. Inactivos o personas con `FechaSalida` no reciben trabajos. Cambiar área, desactivar o registrar salida exige resolver asignaciones activas. El correo opcional no se repite. |
| Asignación | `Detalle_Servicio`, pareja servicio–trabajador. Se retira o reactiva la misma fila. Capacidad máxima de dos servicios activos distintos; retirar al último habilitado de un servicio abierto está prohibido. |
| Consumo | `Productos_Utilizados`, pareja producto–servicio. Cantidad decimal positiva. Alta, diferencia al editar y devolución explícita se guardan junto con Stock. Un reintento no descuenta nuevamente la cantidad completa. |
| Factura | `Factura` y `Detalle_Factura`. Solo servicios finalizados del mismo cliente, al menos un detalle y ninguna segunda factura vigente para el mismo servicio. Cabecera, detalles e importes se guardan juntos. |
| Historial | Facturas anuladas conservan número, cliente, RUC copiado al emitir, detalles e importes. Anular no devuelve existencias. Los consumos de un servicio con historial de facturas no se eliminan. |

Se normalizan espacios exteriores sin restringir tildes, apellidos compuestos o
nombres legítimos. Las longitudes proceden de los atributos existentes de las
entidades. Los importes respetan `decimal(12,2)` y las cantidades/Stock,
`decimal(12,3)`; se rechaza un exceso de decimales o rango antes de escribir.
No se impone unicidad a nombres, teléfonos ni al RUC copiado en facturas.

### Estados conservados

- Servicio: `Abierto`, `Finalizado`, `Cancelado`. El esquema actual no admite
  `Pendiente`; no se agregó, convirtió ni equiparó con otro estado. Los conteos
  contemplan ese literal si aparece en datos previos, pero el sistema no lo crea.
- Asignación: `Activo` y `Retirado`. Solo la asignación vigente de un servicio
  activo ocupa capacidad; cerrar un servicio retira sus asignaciones.
- Factura: `Emitida` y `Anulada`. No se agregaron estados ni edición de facturas.

El formulario de alta exige un trabajador inicial porque un servicio nuevo se
abre directamente. Servicio y asignación inicial se crean en la misma operación.
No había un flujo de reapertura y no se añadió uno. No se convierte un finalizado
en abierto mediante la edición ni se ofrece una corrección que el sistema no tenga.
Cambiar vehículo o tipo de un trabajo con asignaciones, consumos o facturas
reinterpretaría su historial: se rechaza y se indica el motivo.

### Transacciones y concurrencia

`OperacionTaller` inicia una transacción y adquiere un bloqueo exclusivo mediante
`sys.sp_getapplock`, con propietario transaccional, en la base conectada. Todos
los servicios de escritura del taller usan el mismo recurso; después del bloqueo
vuelven a leer el estado persistido. Esto coordina procesos distintos sin crear
objetos SQL y evita la separación entre comprobar capacidad/duplicados y guardar.
Es una estrategia conservadora para este taller: serializa las escrituras de
negocio, con menor paralelismo que bloqueos por recurso. Los listados siguen leyendo.

Además, el descuento de Stock se efectúa con un `UPDATE` condicionado por la
disponibilidad en esa escritura. La edición del catálogo compara además el
saldo original del formulario con Stock persistido; si un consumo lo cambió, se
rechaza el formulario desactualizado en vez de reponer existencias por accidente. Si cualquier validación falla, la transacción
se revierte. Se manejan duplicados, restricciones, conflictos y desbordamientos
con mensajes en español sin publicar consultas ni trazas. La numeración conserva
`FAC-año-secuencia`, compara la secuencia numérica e incluye facturas anuladas.

Estas garantías coordinan las escrituras de esta aplicación. No prometen impedir
escrituras externas que ignoren sus reglas y bloqueos; solo las restricciones ya
existentes de SQL Server pueden proteger esos accesos directos.

### Acceso y entradas

Se conservan cookies, Identity, antiforgery y autorización en los controladores.
No existen roles ni ámbitos de propiedad por usuario en el sistema actual; todos
los usuarios autenticados conservan su acceso interno. No se vinculó `Usuarios`
con clientes o trabajadores. Los controladores usan modelos de entrada y copian
solo campos permitidos; importes y estados de facturación se calculan en servidor.
Las operaciones rápidas ahora comprueban `ModelState` antes de escribir. Los
campos numéricos y fechas obligatorios no aceptan omisiones o conversiones fallidas
como un cero o una fecha predeterminada válidos. Los formularios de alta/edición
conservan sus entradas y los errores se muestran en la página de la operación.

## Límites que se mantienen explícitos

1. `Productos_Utilizados` no guarda precio histórico, fecha del consumo ni un
   historial de correcciones. No hay otro mecanismo persistente adecuado para
   reconstruirlo. Los precios actuales en pantallas se rotulan como referencia o
   estimación, nunca como precio histórico aplicado. No se agregaron columnas ni
   se ocultaron precios en notas, observaciones o archivos.
2. En el código anterior, `Servicio.Costo` ya era el importe cobrado por el trabajo,
   y cada detalle de factura copiaba ese importe. Se conserva esa fórmula: el
   operador debe incluir los productos que correspondan en el monto del trabajo.
   No se suman automáticamente precios actuales, evitando cobrar dos veces o
   inventar un precio aplicado. Un desglose histórico automático de mano de obra
   y productos queda pendiente por falta de información persistente.
3. Se conserva el IVA configurable y el redondeo decimal existente a dos posiciones
   (`ToEven`). No se cambia la tasa. La anulación guarda el motivo completo en
   `Notas`: quedan 191 caracteres después del prefijo existente `Anulada: `.
4. Ya existían consumos en pintura. Se conservan, señalando la discrepancia con
   el alcance de referencia que solo confirma consumos en mantenimiento. No se
   reinterpretaron ni eliminaron los consumos existentes.
5. Los consumos anteriores no descontaban Stock. No existe un marcador para
   distinguirlos ni reconstruir inventario pasado. Se usa el Stock persistido
   como saldo inicial y no se ejecutan ajustes masivos. El profesor debe comprobar
   su saldo de pruebas antes de probar correcciones de consumos antiguos.
6. El SQL conservado exige al menos una identificación de cliente mediante
   `CK_Cliente_Identificacion`; ningún documento individual es obligatorio, pero
   ambos vacíos incumplen esa restricción existente. Si una base instalada aún
   mantiene `RUC NOT NULL` de una versión anterior, registrar sin RUC no puede
   funcionar con ella: no se cambia su nulabilidad ni se inventa un documento.
7. No se reparan datos históricos automáticamente. Estados desconocidos, fechas
   invertidas, campos específicos ausentes y relaciones incompatibles provocan
   errores al intentar la operación correspondiente; no se convierten los datos.
8. La protección de reintentos usa las parejas existentes, estados persistidos y
   cantidades objetivo absolutas: no duplica descuentos, asignaciones ni facturas
   vigentes. Las altas independientes de servicios repetibles y productos sin
   clave natural no tienen un identificador persistente de solicitud. No se
   deduplican por nombre/descripción ni se promete idempotencia universal de esas
   altas entre reinicios o procesos. Para esa garantía faltaría un mecanismo
   persistente de idempotencia que la infraestructura actual no proporciona.

## Pruebas y pasos para el profesor

Archivos de esta intervención (además de conservar los cambios locales previos):

| Grupo | Archivos |
|---|---|
| Reglas y transacciones | `src/Services/ReglasTaller.cs`, `OperacionTaller.cs`, `TrabajadorService.cs` (nuevos); `ClienteService.cs`, `VehiculoService.cs`, `ServicioService.cs`, `AsignacionService.cs`, `ProductoService.cs`, `FacturacionService.cs`. |
| Consultas coherentes | `src/Services/DashboardService.cs`, `ReporteService.cs`. |
| Entrada y arranque | `src/ViewModels/OperacionViewModels.cs`, `src/Data/DatabaseInitializer.cs`, `src/Program.cs`. |
| Controladores | `ClientesController.cs`, `VehiculosController.cs`, `ServiciosController.cs`, `AsignacionesController.cs`, `TrabajadoresController.cs`, `ProductosController.cs`, `FacturasController.cs` en `src/Controllers/`. |
| Formularios y mensajes | `src/Views/Servicios/{Crear,Editar,Detalles}.cshtml`, `Asignaciones/Index.cshtml`, `Productos/{Editar,Detalles}.cshtml`, `Facturas/{Crear,Detalles}.cshtml`. |
| Pruebas | `tests/PruebasValidacionesTaller.cs`, `PruebasEntradasMvc.cs` (nuevos); `FabricaDeContextos.cs`, `PruebasUnitariasReglas.cs`, `PruebasSimplificacion.cs`, `PruebasIdentificacionCliente.cs`, `PruebasTraduccionSql.cs`, `PruebasIntegracionSqlServer.cs`. |
| Documentación | `README.md`, este documento. |

Compilar y ejecutar las pruebas sin conexión SQL:

```bash
dotnet build TallerMecanicoSantoDomingo.sln
dotnet test TallerMecanicoSantoDomingo.sln --filter 'Categoria!=Integracion'
```

Para concurrencia real se necesita una base **de pruebas ya existente**, con el
esquema terminado y sin datos reales. Los tests no crean ni alteran su estructura.
No utilice la conexión del taller: los INSERT de pruebas pueden avanzar IDENTITY
incluso cuando se revierte una transacción o se limpian las filas de prueba.

```bash
export TALLERMECANICO_CADENA_PRUEBAS='CADENA_DE_LA_BASE_DE_PRUEBAS'
export TALLERMECANICO_PRUEBAS_ESCRITURA=1
dotnet test TallerMecanicoSantoDomingo.sln --filter 'Categoria=Integracion'
```

No se leen automáticamente secretos ni variables de conexión de producción.
Las pruebas SQL incluyen tres asignaciones simultáneas, dos consumos que compiten
por existencias, reintentos de cantidades, dos emisiones para el mismo servicio y
emisiones simultáneas de servicios distintos con números diferentes.

Pruebas manuales, sobre esa misma base de pruebas:

| Paso | Caso válido | Caso que debe rechazarse |
|---|---|---|
| 1 | Registrar particular con cédula y RUC vacío. | Otra persona con la misma cédula; documentos demasiado largos. |
| 2 | Editar un vehículo conservando su placa. | Registrar otro vehículo con esa placa o cambiar propietario si ya tiene servicios. |
| 3 | Crear mantenimiento con falla, fecha, monto y mecánico inicial. | Sin personal, con pintor, con trabajador inactivo/con salida o falla vacía. |
| 4 | Abrir segundo trabajo del mismo trabajador. | Un tercero activo; editar sin cambiar la asignación sí debe funcionar. |
| 5 | Asignar reemplazo compatible y retirar al anterior. | Retirar al último habilitado; cambiar área/salida/estado del trabajador antes de resolver sus trabajos. |
| 6 | Crear pintura con tipo y color. | Tipo o color vacíos, también enviando una solicitud manual. |
| 7 | Consumir exactamente Stock, incluso una cantidad decimal. | Cero, negativo, exceso, producto inactivo o más de tres decimales. |
| 8 | Cambiar cantidad de 2 a 3 descuenta solo 1. | Repetir el alta no debe descontar de nuevo; repetir cantidad 3 no debe descontar nada adicional. |
| 8b | Editar inventario desde un formulario recién cargado. | Abrir la edición, consumir desde otra pestaña y enviar el formulario antiguo: no debe sobrescribir el nuevo Stock. |
| 9 | Finalizar con fecha igual o posterior al inicio. | Fecha ausente/invertida o facturar un abierto. |
| 10 | Facturar varios vehículos del mismo cliente. | Factura vacía, mezclar clientes o volver a facturar un servicio con factura vigente. |
| 11 | Cambiar precio de catálogo después de emitir. | El importe guardado de la factura debe permanecer igual; no editar consumos/costo/tipo del servicio facturado. |
| 12 | Anular con motivo y consultar la factura anulada. | No deben desaparecer detalles, reutilizarse el número ni reponerse Stock. |
| 13 | Iniciar sesión y usar formularios con token. | Sin autenticación o sin token antiforgery; cantidades o fechas manipuladas no deben producir escrituras. |

Resultados de esta ejecución:

- `dotnet build`: correcto, sin advertencias ni errores.
- `dotnet test`: 84 pruebas aprobadas y 8 de SQL Server omitidas; no había una
  base de pruebas SQL apropiada disponible y no se creó ni alteró ninguna.
- Navegador contra un host temporal con los controladores/vistas del proyecto y
  EF InMemory: sesión, antiforgery, solicitudes inválidas, personal, existencias,
  facturación y anulación; 36 comprobaciones de páginas a 1440, 390 y 320 píxeles
  sin desbordamientos exteriores ni errores JavaScript.
- Comparación de contenido contra el inicio de la intervención: scripts SQL,
  entidades, mapeos y dependencias conservados exactamente.

Las comprobaciones HTTP con EF InMemory validan presentación y acciones, no la
concurrencia de SQL Server. Esa ejecución real queda pendiente con la conexión
de pruebas indicada arriba. No se modificaron datos reales del taller.
