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
  practica.sql                   Diez ejercicios de consultas sin soluciones
  AnalisisBaseDatos.md            Relaciones, datos y restricciones del esquema
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

Las tres tablas intermedias usan claves primarias compuestas. Cada vehículo
pertenece a un cliente, cada servicio a un vehículo y cada factura a un cliente.
Las relaciones impiden borrar datos que tienen historial asociado.

Los formularios permiten registrar, editar y consultar la cédula del cliente,
el kilometraje, color, chasis y tipo del vehículo, el correo y las fechas de
ingreso y salida del trabajador, y el costo de compra del producto.
`Trabajador.Estado` indica si está activo y `Trabajador.Area` almacena su especialidad.

En C#, `Cliente.RFC` se mapea a `RUC`, `PrecioUnitario` a `Precio`,
`Servicio.Total` a `Costo`, `FechaFactura` a `Fecha` y
`Trabajador.FechaRegistro` a `FechaIngreso`. Los formularios muestran «RUC» y
la factura usa el valor fiscal guardado al emitirla.

El script SQL es la fuente del esquema; **no se usan migraciones de EF Core**.
`TallerMecanicoSantoDomingo.sql` contiene `CREATE DATABASE`, `CREATE TABLE`,
claves, validaciones y el procedimiento de anulación. Se puede ejecutar una sola
vez desde un editor de SQL para crear la base, o dejar que la aplicación la cree
en el primer arranque.
`DatosDemostracion.sql` contiene los `INSERT INTO ... VALUES`; se usa una sola vez
sobre el esquema vacío. `Consultas.sql` contiene únicamente consultas `SELECT`
para ver los datos y ejemplos de relaciones y totales; puede repetirse.
`AnalisisBaseDatos.md` describe las tablas y los datos de demostración;
`practica.sql` contiene diez ejercicios de consultas, de menor a mayor dificultad,
con espacio para resolverlos y sin soluciones incluidas.

Si la base ya contiene las tablas y columnas del diagrama, el primer arranque
agrega las restricciones `CHECK` y el procedimiento que falten en una transacción.
La cuenta de SQL Server necesita permisos de DDL para esa actualización. Si una
fila incumple una restricción, SQL Server identifica la restricción y revierte
la actualización. La carga de datos de demostración es manual y opcional.

Una base antigua con tablas como `Clientes`, `Areas` o `AspNetUsers`, o con
columnas faltantes, requiere una migración específica. El arranque identifica
el esquema incompleto antes de ejecutar la actualización.

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
