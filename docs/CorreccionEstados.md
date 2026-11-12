# Revisión de desactivación y reactivación

## Fallo reproducido y causa

Ambos problemas seguían presentes. Los campos ocultos de los listados pasaban
un booleano directamente al atributo `value` de Razor. El HTML generado era:

```html
<!-- Intento de desactivar -->
<input type="hidden" name="activo">
<!-- Intento de reactivar -->
<input type="hidden" name="activo" value="value">
```

Ninguno enviaba un booleano válido. Los controladores rechazaban correctamente
la entrada y no llegaban a ejecutar la escritura. El síntoma era que la acción
no cambiaba el estado ni se reflejaba en los datos.

Se corrigieron Clientes, Vehículos, Trabajadores y Productos para que el HTML
envíe las cadenas literales `false` y `true`. Se conservan las confirmaciones,
antiforgery, validación del servidor y reglas que pueden impedir una desactivación
válidamente, como tener trabajos activos.

| Módulo | Columna existente donde se guarda |
|---|---|
| Clientes | `dbo.Cliente.Activo`: 0 desactivado, 1 activo |
| Vehículos | `dbo.Vehiculo.Activo`: 0 desactivado, 1 activo |
| Productos | `dbo.Producto.Activo`: 0 desactivado, 1 activo |
| Trabajadores | `dbo.Trabajador.Estado`: 0 desactivado, 1 activo; en C# se llama `Activo` |

## Otros errores corregidos

- Los clientes desactivados desaparecían del filtro de vehículos. Ahora siguen
  disponibles para consultar historial, identificados como desactivados.
- Editar un vehículo de un cliente desactivado perdía la selección del propietario
  y el servidor rechazaba incluso conservarlo. Ahora se conserva ese propietario
  y se permite corregir la ficha. Registrar o transferir vehículos a un cliente
  desactivado sigue rechazándose.
- El alta de servicios ofrecía vehículos cuyo cliente estaba desactivado, aunque
  el servidor no permitía abrirles trabajos. El selector ahora coincide con esa
  validación. La edición conserva sus opciones existentes.
- Un trabajador inactivo o con fecha de salida podía figurar como disponible solo
  por tener cero trabajos. Listado, ficha y reporte ahora respetan su habilitación.

## Archivos de esta revisión

- `src/Views/{Clientes,Vehiculos,Productos,Trabajadores}/Index.cshtml`.
- `src/Views/Trabajadores/Detalles.cshtml`, `src/Views/Reportes/Trabajadores.cshtml`.
- `src/Controllers/VehiculosController.cs`, `src/Controllers/ServiciosController.cs`.
- `src/Services/VehiculoService.cs`, `src/Services/ReporteService.cs`.
- `tests/PruebasCambiosEstadoMvc.cs` (nuevo), `tests/PruebasIntegracionSqlServer.cs`.
- Este documento y el enlace desde `README.md`.

## Verificación y reproducción

Las pruebas HTTP usan Razor, los controladores y el binding reales con EF
InMemory y una identidad de pruebas. Comprueban dos ciclos de desactivación y
reactivación por módulo, leen cada estado desde otro contexto, rechazan valores
inválidos y modificaciones sin antiforgery, y cubren los errores relacionados.
No constituyen una comprobación de persistencia en SQL Server.

También hay cuatro casos SQL, uno por módulo, que leen el estado desde otra
conexión después de cada cambio. Requieren una base de pruebas ya existente,
`TALLERMECANICO_CADENA_PRUEBAS` y `TALLERMECANICO_PRUEBAS_ESCRITURA=1`. No se
habilitan con la conexión de producción ni crean el esquema.

Para probar manualmente sobre datos de prueba:

1. Desactivar desde el listado, confirmar y recargar. Debe mostrarse desactivado
   y el botón debe ofrecer reactivar. La columna correspondiente debe valer 0.
2. Reactivar, confirmar y recargar. Debe mostrarse activo y la columna debe valer 1.
3. Cancelar la confirmación: no debe enviarse la solicitud ni cambiar el estado.
4. Desactivar un cliente y consultar sus vehículos usando el filtro. Editar la
   ficha de uno conservando su propietario debe funcionar; crearle otro vehículo
   o abrirle un nuevo servicio debe exigir primero reactivar al cliente.
5. Desactivar un trabajador sin trabajos activos: su estado y su disponibilidad
   deben coincidir. Una fecha de salida también debe impedir mostrarlo disponible.

El SQL, los modelos de entidades, los mapeos, el inicializador y las dependencias
se conservaron exactamente como estaban al comenzar esta revisión. No se hicieron
cambios de estructura ni se modificaron datos reales del taller.

Resultado de la revisión: compilación sin errores ni advertencias, 92 pruebas
aprobadas y 12 casos SQL omitidos por falta de una base de pruebas disponible.
En navegador, con cookies y EF InMemory, pasaron los ciclos de estado de los
cuatro módulos, aceptar/cancelar confirmaciones, recargar y comprobar los valores
enviados. También pasó el flujo de servicios, consumo, factura, anulación y 36
comprobaciones de páginas a diferentes anchos. La persistencia directa en SQL
Server queda pendiente de ejecutar con la conexión de pruebas indicada arriba.
