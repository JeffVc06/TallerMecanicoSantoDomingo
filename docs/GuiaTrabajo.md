# Guía del trabajo diario del taller

## Abrir un trabajo

1. En **Servicios → Registrar servicio**, elija el vehículo por su placa.
2. Elija mantenimiento mecánico o pintura antes de seleccionar al responsable.
   La lista muestra personal habilitado del área elegida, con menos de dos trabajos
   activos. Si no hay cupo, revise las asignaciones o el personal del taller.
3. Para mantenimiento, describa la falla. Para pintura, indique tipo de pintura y
   color. Complete la descripción del trabajo, la fecha y el importe.
4. Pulse **Abrir servicio**: se guarda el trabajo con su asignación inicial.

## Mientras el vehículo está en atención

- La ficha muestra la placa junto al número de servicio. Los accesos a personal,
  productos y cierre llevan a la sección correspondiente del mismo trabajo.
- Puede agregar personal compatible. Para retirar al último responsable, asigne
  primero un reemplazo habilitado. El historial de asignaciones se conserva.
- El vehículo y el tipo quedan establecidos al abrir el trabajo. Al editar, puede
  corregir descripción, falla, datos específicos, fecha e importe; los controles
  reflejan las restricciones que ya aplica el servidor.
- Registre únicamente productos realmente utilizados. Las cantidades admiten
  decimales y su consumo descuenta existencias según las reglas existentes.
- El **importe completo del trabajo** es lo que se cobrará: incluya los productos
  que correspondan. Registrar un consumo no suma automáticamente su precio al cobro.
  Las estimaciones a precio actual son referencias, no precios históricos.

## Terminar y facturar

- **Finalizar trabajo** exige una fecha válida, cierra las asignaciones y deja el
  servicio finalizado. Revise personal, productos e importe antes de confirmar.
- En la ficha finalizada aparece **Preparar factura**. Abre los servicios
  finalizados del mismo cliente; elija los que correspondan antes de emitir.
- Si ya existe una factura vigente, la ficha ofrece consultarla y protege las
  operaciones que el servidor ya bloquea.
- **Cancelar trabajo** conserva el historial y cierra las asignaciones. Los
  productos consumidos permanecen descontados; no se devuelve inventario por
  cancelar. Las correcciones explícitas siguen las operaciones existentes.

## Consultar y continuar

- El listado de servicios muestra los nombres del personal. En trabajos cerrados,
  indica que se trata del personal del historial, sin presentarlo como asignación vigente.
- El historial conserva la placa elegida después de consultar y avisa si un enlace
  apunta a un vehículo inexistente.
- Activar o desactivar desde un listado conserva la búsqueda, la página y el filtro
  de cliente cuando corresponde.
- En teléfonos, personal y productos se muestran como tarjetas; los controles para
  corregir cantidades y retirar personal permanecen accesibles. La fecha de cierre
  ocupa su propio espacio para que se lea completa.

## Revisión técnica de esta entrega

Se corrigió la paginación fuera de rango antes de calcular el desplazamiento y se
agregaron desempates de orden para páginas estables. Los servicios cerrados o
facturados no ofrecen nuevas asignaciones ni formularios de edición inválidos.
Los filtros visuales de personal y los botones deshabilitados complementan las
validaciones transaccionales del servidor; no las sustituyen.

Se conservaron el SQL, las entidades, los mapeos, el inicializador, la autenticación,
las dependencias, los estados persistidos y la fórmula de facturación. No se
repararon datos históricos automáticamente ni se modificaron datos reales.

Las pruebas SQL requieren una base de pruebas ya preparada; las comprobaciones
HTTP y de navegador con EF InMemory no demuestran concurrencia real de SQL Server.
