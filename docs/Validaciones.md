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

Se recortan los textos generales. Los nombres admiten letras Unicode, tildes,
espacios, guiones y apóstrofos entre palabras. El teléfono exige el formato
estricto descrito abajo. Las longitudes de los demás campos proceden de los atributos de las
entidades. Los importes respetan `decimal(12,2)` y las cantidades/Stock,
`decimal(12,3)`; se rechaza un exceso de decimales o rango antes de escribir.
No se impone unicidad a nombres ni al RUC copiado en facturas.

### Revisión de identificaciones, duplicados y contacto

El dato que puede llamarse informalmente «carnet» aparece como **Cédula** del
cliente. No existe un campo separado de carnet, ni una cédula de trabajador
en el modelo o formulario actual.

| Dato | ¿Se rechaza si pertenece a otro registro? | Alcance |
|---|---|---|
| Cédula de cliente | Sí, al crear y editar, incluidos clientes desactivados. | Se recortan espacios exteriores y se convierte a mayúsculas; índice único filtrado en el SQL del proyecto. |
| RUC de cliente | Sí, al crear y editar, incluidos clientes desactivados. | Misma normalización; índice único filtrado en el SQL del proyecto. |
| Teléfono de cliente o trabajador | **Sí**, al crear y editar, dentro de cada módulo y entre ambos, incluidos registros inactivos. | Exactamente 8 dígitos ASCII consecutivos (`88881111`), sin espacios, guiones ni prefijos. Se valida en formularios y servicios; la comprobación de duplicados y la escritura usan el bloqueo transaccional común. No hay índice único de teléfono. |
| Nombre y apellido | No. | Obligatorios, hasta 100 caracteres; letras, tildes, espacios, guiones y apóstrofos entre palabras. Se rechazan números y otros signos. |
| Correo de trabajador | Sí, cuando tiene valor, incluidos trabajadores inactivos. | Se recorta y convierte a minúsculas, también al comparar valores previos. Exige usuario y dominio con punto; sin espacios, puntos consecutivos ni etiquetas de dominio que empiecen o terminen con guion. Sin índice único de correo. |
| Placa y chasis | Sí; el chasis solo cuando tiene valor. | Solo caracteres alfanuméricos ASCII, con espacios o guiones como separadores. Al comparar se ignoran esos separadores y las mayúsculas, incluso con vehículos inactivos; los índices existentes conservan su comparación original. |
| RUC copiado en factura | No. | Un cliente puede tener varias facturas que conserven el mismo RUC. |

Editar un registro conservando sus propios documentos, teléfono, placa, chasis o correo
no lo considera duplicado: las consultas excluyen su identificador. Desactivar
al titular no libera sus valores únicos para un registro nuevo.

La cédula y el RUC admiten hasta 20 caracteres y se exige al menos uno de los
dos. **No se valida su autenticidad ni una máscara exacta**; tampoco se eliminan
guiones o espacios interiores para comparar variantes. Por ejemplo, no se
equipara automáticamente `001-120690-0001A` con `0011206900001A`. La unicidad
se aplica a los valores comparados tras la normalización descrita, no a todas
las representaciones posibles de un documento.

La validación telefónica **exige exactamente ocho dígitos**. `88881111` es un
formato válido; `8888`, `888811111`, `8888-1111`, `+505 8888-1111`, espacios y
letras se rechazan. Los ceros iniciales se conservan. El número no puede estar
asignado a otra ficha de cliente o trabajador, aunque esté desactivada; editar
la propia ficha sin cambiar el número sí está permitido.

Para comparar teléfonos previos se quitan espacios, guiones, puntos y paréntesis,
y se reconocen los prefijos `+505` y `00505`. Así, una ficha antigua con
`8888-1111` o `+505 8888-1111` impide registrar `88881111` en otra ficha. No se
modifican ni corrigen datos históricos automáticamente: al editarlos se debe
introducir el formato de ocho dígitos y resolver los duplicados que ya existan.
Esta comprobación no acredita que el número exista o pertenezca a la persona.
La regla se aplica en la aplicación sin modificar el esquema de la base.

### Validaciones adicionales de captura

- Nombres y apellidos: `José Ángel`, `María-José`, `O'Connor` y `D’Ávila` son
  válidos; `Ana123` o `***` se rechazan tanto en formularios como en servicios.
  Esto no impone unicidad a los nombres.
- Dirección, nombres de productos, descripciones, marca, modelo, color y tipo:
  cuando tienen valor deben contener alguna letra o número. Se rechazan textos
  formados solo por signos o caracteres invisibles, y controles como el carácter
  nulo. Las descripciones pueden conservar saltos de línea y referencias como
  `10W-30`; los campos opcionales pueden quedar vacíos.
- Placa y chasis: `MGA-1234`, `mga1234` y `MGA 1234` se comparan como la misma
  clave. No se pueden reutilizar en otro vehículo ni estando desactivado el
  titular, y se puede conservar la clave propia al editar. No se impone una
  longitud de VIN ni se comprueba la autenticidad del identificador. Esta
  comparación no cambia la regla de cédulas/RUC descrita anteriormente.
- Año del vehículo: desde 1950 hasta el año siguiente al actual, tanto en el
  formulario como al guardar. El máximo de la vista se calcula en cada petición.
- Correo: `nombre+avisos@sub.dominio.com` es válido; `ana@dominio`,
  `ana..lopez@dominio.com` y `ana@-dominio.com` se rechazan. Sigue siendo opcional.
  Se comprueba su estructura de captura, sin consultar si el dominio o buzón existen.
- Importes y cantidades: los formularios muestran errores en el campo de precio,
  costo o importe si se exceden dos decimales; Stock y consumos admiten tres.
  Se mantienen los controles de rango y signo de los servicios, sin redondear
  silenciosamente una entrada que perdería precisión.
- Selecciones: los identificadores seleccionados deben ser positivos; área,
  tipo de servicio y método de pago deben corresponder a opciones permitidas.
  Una factura necesita al menos un servicio y rechaza identificadores repetidos
  dentro de la misma solicitud, antes de crear cabecera o detalles.

La [guía de defensa CRUD](GuiaDefensaCRUD.txt) incluye estas respuestas y una
secuencia para demostrar duplicados, edición y teléfonos de ocho dígitos únicos.

Verificación de esta revisión (13 de noviembre de 2026):

- Se actualizaron los casos de `tests/PruebasDuplicadosYContacto.cs`: documentos
  duplicados, teléfonos únicos al crear/editar entre ambos módulos con titulares
  activos o inactivos, conservación del número propio, rechazo de formatos
  diferentes de ocho dígitos y comparación de formatos telefónicos previos.
  Se mantienen los casos de documentos sin máscara y con/sin guiones admitidos.
- `tests/PruebasFormatosEntrada.cs` comprueba nombres, textos con contenido,
  correos, placas/chasis equivalentes, años, precisión y selecciones de factura,
  incluidas altas/ediciones válidas y conservación de los datos ante un rechazo.
- Las comprobaciones de contacto utilizan EF InMemory y validación de formularios;
  las de traducción comprueban las consultas del proveedor SQL Server sin conexión.
  No se ejecutan escrituras sobre una base instalada para esta revisión.
- Las pruebas HTTP comprueban altas/ediciones de contacto, vehículos y productos,
  y una factura con servicios repetidos. Los errores se muestran en la vista,
  se conserva el dato persistido y se permite corregir y volver a enviar.
- `dotnet test TallerMecanicoSantoDomingo.sln --no-restore --filter 'Categoria!=Integracion' --verbosity minimal`:
  **219 pruebas aprobadas, 0 fallidas**.

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

Resultados de la intervención inicial (anteriores a la revisión de contacto):

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
