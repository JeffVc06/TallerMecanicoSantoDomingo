# Taller Mecánico Santo Domingo

Aplicación web para clientes, vehículos, trabajadores, servicios, productos y
facturación. Usa C#, ASP.NET Core MVC, .NET 10 y SQL Server. Las pantallas son
Razor con Bootstrap y las librerías se incluyen en `wwwroot/lib`.

## Cómo está organizado

```text
TallerMecanicoSantoDomingo.sln
src/
  Program.cs               Arranque, acceso y servicios
  Controllers/             Reciben los formularios y muestran las páginas
  Services/                Reglas del taller y consultas
  Models/                  Datos del diagrama
  ViewModels/              Datos y validaciones de los formularios
  Data/                    Conexión, esquema y almacenamiento de usuarios
  Views/                   Pantallas
  wwwroot/                 Estilos, JavaScript y librerías
  appsettings.json         Configuración común, sin contraseñas
  TallerMecanicoSantoDomingo.csproj
database/
  TallerMecanicoSantoDomingo.sql   Tablas y procedimiento de anulación
  DatosDemostracion.sql           Datos de ejemplo opcionales
  Consultas.sql                  Consultas de lectura y reportes de ejemplo
tests/                     Pruebas de negocio, acceso e integración
```

Una solicitud sigue este recorrido: **pantalla → controlador → servicio → base
de datos**. Los servicios son clases concretas; no hay repositorios ni interfaces
que repitan sus métodos. El contexto utiliza los atributos de los modelos y solo
configura explícitamente las relaciones y restricciones.

## Base de datos y diagrama

El esquema contiene las nueve tablas del diagrama y una tabla de cuentas:

| Tabla | Contenido |
|---|---|
| Cliente | Identificación, nombre, teléfono y RUC |
| Vehiculo | Datos del vehículo y su propietario |
| Trabajador | Personal, contacto, estado y fechas |
| Servicio | Trabajo realizado a un vehículo |
| Detalle_Servicio | Pareja servicio–trabajador |
| Producto | Repuestos, precio de venta y costo |
| Productos_Utilizados | Pareja producto–servicio y cantidad |
| Factura | Fecha, datos fiscales, IVA e importe |
| Detalle_Factura | Pareja servicio–factura y subtotal |
| Usuarios | Cuentas que pueden entrar al sistema |

Las tres tablas intermedias usan claves primarias compuestas. No necesitan un
identificador adicional. Cada vehículo pertenece a un cliente; cada servicio a
un vehículo. Las relaciones impiden borrar datos que tienen historial asociado.

Se mantienen algunas columnas adicionales para conservar las pantallas y las
funciones existentes: apellidos, dirección, stock, especialidad del trabajador,
estados de asignación, observaciones, número y método de pago de la factura,
y campos de auditoría/desactivación. La relación `Factura.IdCliente` evita perder
la asociación original al cliente. No hay una tabla `Areas`: la especialidad se
guarda directamente en `Trabajador.Area` con dos valores permitidos.

Los campos del diagrama que no tienen controles en las pantallas actuales
(cédula, kilometraje, color y chasis del vehículo, correo y salida del trabajador,
y costo de compra del producto) están en el esquema y en los modelos, como datos
opcionales o con valor inicial. No se añadieron controles ni pantallas.
`Trabajador.Estado` es un indicador activo/inactivo, como en la interfaz existente.

Para conservar los formularios actuales, algunos nombres de C# se mapean a los
del diagrama: `RFC` → `RUC`, `PrecioUnitario` → `Precio`, `Servicio.Total` → `Costo`,
`FechaFactura` → `Fecha` y `Trabajador.FechaRegistro` → `FechaIngreso`.
`Factura.RUC` conserva la identificación fiscal del cliente al emitir la factura.

El script SQL es la fuente del esquema; **no se usan migraciones de EF Core**.
`TallerMecanicoSantoDomingo.sql` contiene `CREATE DATABASE`, `CREATE TABLE`,
claves y validaciones. Se ejecuta una sola vez sobre una base nueva.
`DatosDemostracion.sql` contiene los `INSERT INTO ... VALUES`; se usa una sola vez
sobre el esquema vacío. `Consultas.sql` contiene únicamente consultas `SELECT`
para ver los datos y ejemplos de relaciones y totales; puede repetirse.

La aplicación comprueba si las tablas ya existen antes de ejecutar el archivo de
creación. No carga datos de demostración ni modifica columnas de tablas existentes.
Los cambios futuros de estructura requieren un script explícito de actualización.

**Bases anteriores:** esta versión cambia nombres y claves. Si ya existe el
esquema anterior (`Clientes`, `Areas`, `AspNetUsers`, etc.) o está incompleto,
el arranque se detiene con un mensaje antes de crear tablas nuevas. Hay que
preparar su migración por separado si contiene información que se deba conservar. Este cambio de código
no ejecuta ni borra nada en el servidor de desarrollo.

## Usuarios y trabajadores

Se recomienda mantenerlos separados. Un trabajador representa a un empleado;
un usuario representa a quien puede iniciar sesión. Un empleado puede trabajar
en el taller sin tener acceso al programa, y el administrador puede tener una
cuenta sin aparecer como mecánico.

`Usuarios` es la única tabla de autenticación. `UsuarioStore` adapta su
almacenamiento a ASP.NET Core Identity. **Identity mantiene el hash de las
contraseñas, el bloqueo tras cinco intentos fallidos, las cookies y el sello de
seguridad**. No se implementan contraseñas ni cifrado propios. No se necesitan
tablas de roles, acceso con proveedores externos ni permisos por usuario porque
la aplicación no ofrece esas funciones.

La página de configuración inicial permite crear la primera cuenta y se cierra
cuando ya existe un usuario. No se incluyen usuarios ni claves predeterminados.
La contraseña de una cuenta del sistema es independiente del acceso a SQL Server.

## Reglas que se conservan

- Identificación fiscal y placas únicas; relaciones entre clientes y vehículos.
- Servicios de mantenimiento o pintura con fechas e importes válidos.
- Personal compatible con el servicio; máximo dos asignaciones activas por
  trabajador. La asignación usa una transacción y bloqueo por trabajador.
- Productos sin duplicados por servicio, cantidades positivas y precios válidos.
- Facturas con servicios del mismo cliente, sin facturarlos dos veces mientras
  exista una factura emitida. Subtotal, IVA y total se calculan en el servidor.
- Anulación sin borrar facturas ni detalles. El procedimiento SQL usa una sola
  actualización; los servicios quedan disponibles para volver a facturarse.
- Historial, reportes, exportación CSV, impresión y desactivación lógica.

## Requisitos y comprobaciones

Se necesita el SDK de .NET 10 y SQL Server accesible por TCP. La conexión se
configura localmente mediante `user-secrets` o `ConnectionStrings__TallerDB`;
las contraseñas no se guardan en el repositorio. La base se llama exactamente
`TallerMecanicoSantoDomingo`, igual que en el script.

La preparación del entorno y la conexión se realizan por separado. Para revisar
el código sin conectar una base se pueden ejecutar las pruebas en memoria:

```bash
dotnet test TallerMecanicoSantoDomingo.sln --filter 'Categoria!=Integracion'
```

Las pruebas de integración solo se habilitan con la variable
`ConnectionStrings__TallerDB` o `TALLERMECANICO_CADENA` apuntando a una base de
pruebas preparada. No leen automáticamente los secretos de la aplicación. Crean
y eliminan sus propios registros; no deben apuntar a datos de producción.

## Trabajo en equipo

Se comparten el código, los scripts y los cambios de estructura mediante Git.
Cada integrante mantiene su contenedor, datos y conexión local. Los cambios de
una tabla deben acompañarse de sus ajustes en modelos y consultas. Las vistas,
el CSS y el JavaScript se mantienen separados de esos cambios de datos.

## Editar con recarga automática

Desde la raíz del proyecto, con SQL Server encendido y la conexión configurada:

```bash
dotnet watch --project src
```

Abra `http://localhost:5080` y guarde los cambios para verlos en el navegador.
`dotnet watch` aplica Hot Reload o reinicia cuando el cambio lo requiere. Si
solicita reiniciar, acepte en la terminal. Los cambios de estructura de la base
requieren su script SQL correspondiente.

También puede ejecutar la tarea `dev` desde **Terminal → Ejecutar tarea** en
VS Code. Detenga cualquier instancia anterior con Ctrl+C para liberar el puerto.
