# Taller Mecánico Santo Domingo

Sistema web de gestión para un taller mecánico (vehículos, servicios, trabajadores,
repuestos y facturación), desarrollado en **ASP.NET Core MVC (.NET 10)** con
**SQL Server** y **Entity Framework Core**.

El modelo de datos se construyó a partir del documento **"MER y Relacional"**
(entidades CLIENTE, VEHÍCULO, SERVICIO, TRABAJADOR, DETALLE_SERVICIO, REPUESTO,
PRODUCTOS_UTILIZADOS, FACTURA, DETALLE_FACTURA y ÁREA DE TRABAJO) y aplica las
**14 reglas de negocio** indicadas en dicho documento.

---

## 1. Tecnologías utilizadas

| Capa | Tecnología |
|------|------------|
| Lenguaje / Servidor | C# 13 · ASP.NET Core MVC · .NET 10 |
| Base de datos | SQL Server (Microsoft SQL Server) |
| Acceso a datos | Entity Framework Core 10 (proveedor SqlServer) |
| Seguridad | ASP.NET Core Identity + cookies seguras + antiforgery (CSRF) |
| Vistas | Razor (.cshtml) + Tag Helpers |
| Front-end | Bootstrap 5.3, Bootstrap Icons, Chart.js, jQuery + jQuery Validation |
| Pruebas | xUnit (unitarias con proveedor InMemory + integración contra SQL Server) |

Todas las librerías de front-end están **descargadas localmente** en
`wwwroot/lib`, por lo que la aplicación funciona sin conexión a Internet.

---

## 2. Requisitos previos

- **.NET SDK 10** (`dotnet --version` debe mostrar `10.x`).
- **SQL Server** en ejecución (Express, Developer o superior) accesible como
  `localhost` con autenticación de Windows, o bien una instancia con usuario y
  contraseña.
- **sqlcmd** (opcional, para ejecutar los scripts SQL manualmente).

Verificación rápida:

```powershell
dotnet --version
sqlcmd -S localhost -E -C -Q "SELECT @@VERSION"
```

---

## 3. Estructura del proyecto

```
TallerMecanicoSantoDomingo/
├─ TallerMecanicoSantoDomingo.sln          Solución (app + pruebas)
├─ README.md
├─ database/
│  ├─ TallerMecanicoSantoDomingo.sql       Esquema completo (fuente única de verdad)
│  └─ DatosDemostracion.sql                Datos de ejemplo (NO se cargan solos)
├─ src/TallerMecanicoSantoDomingo/
│  ├─ Program.cs                           Configuración, seguridad e inyección de dependencias
│  ├─ appsettings.json                     Cadena de conexión y configuración del taller
│  ├─ Controllers/                          Account, Dashboard, Clientes, Vehiculos,
│  │                                        Servicios, Trabajadores, Asignaciones,
│  │                                        Productos, Facturas, Historial, Reportes
│  ├─ Data/
│  │  ├─ TallerDbContext.cs                Mapeo EF Core (replica el script SQL)
│  │  └─ DatabaseInitializer.cs            Crea la BD/áreas y el primer administrador
│  ├─ Models/                              Entidades del MER
│  ├─ Services/                            Lógica de negocio y reglas 1–14
│  ├─ ViewModels/                          Modelos de formulario y de cuenta
│  ├─ Views/                               Vistas Razor (Tag Helpers)
│  └─ wwwroot/                             CSS, JS e imágenes + librerías locales
└─ tests/TallerMecanicoSantoDomingo.Tests/ Pruebas unitarias y de integración
```

---

## 4. Puesta en marcha (comandos exactos)

Abra **PowerShell** y ejecute, en orden:

```powershell
# 1) Ir a la carpeta del proyecto
cd C:\Users\JeffWin\Desktop\TallerMecanicoSantoDomingo

# 2) Restaurar paquetes NuGet
dotnet restore TallerMecanicoSantoDomingo.sln

# 3) Compilar
dotnet build TallerMecanicoSantoDomingo.sln

# 4) Ejecutar (crea la base de datos automáticamente la primera vez)
dotnet run --project src\TallerMecanicoSantoDomingo\TallerMecanicoSantoDomingo.csproj --urls http://localhost:5080
```

Abra el navegador en **http://localhost:5080**.

> La aplicación **crea sola la base de datos** `TallerMecanicoSantoDomingo` y su
> esquema la primera vez que arranca (leyendo el script incrustado
> `database/TallerMecanicoSantoDomingo.sql`). No se requieren migraciones de EF.

### 4.1. Crear la base de datos manualmente (alternativa con sqlcmd)

Si prefiere crear el esquema y cargar los datos de ejemplo "a mano":

```powershell
# Esquema (crea la base de datos y todas las tablas, índices y restricciones)
sqlcmd -S localhost -E -C -b -f 65001 -i database\TallerMecanicoSantoDomingo.sql

# Datos de demostración (opcional; el script no se ejecuta automáticamente)
sqlcmd -S localhost -E -C -b -f 65001 -d TallerMecanicoSantoDomingo -i database\DatosDemostracion.sql
```

> **Importante:** la opción `-f 65001` fuerza la lectura de los scripts en UTF-8
> para que los acentos (`mecánico`, `Pérez`, etc.) se guarden correctamente.
> Si ejecuta los scripts sin ella, use una versión de `sqlcmd` que detecte el BOM
> UTF-8 (incluido en los archivos) o los textos acentuados se corromperán.

Para empezar de cero (borra y recrea):

```powershell
sqlcmd -S localhost -E -C -Q "ALTER DATABASE [TallerMecanicoSantoDomingo] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [TallerMecanicoSantoDomingo];"
```

### 4.2. Cadena de conexión

El valor predeterminado está en `src/TallerMecanicoSantoDomingo/appsettings.json`:

```
Server=localhost;Database=TallerMecanicoSantoDomingo;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=true
```

Puede sobreescribirse sin tocar el código con la variable de entorno
`ConnectionStrings__TallerDB`, por ejemplo para usar usuario y contraseña:

```powershell
$env:ConnectionStrings__TallerDB = "Server=localhost;Database=TallerMecanicoSantoDomingo;User Id=sa;Password=SU_CONTRASENA;TrustServerCertificate=True"
```

---

## 5. Crear el primer administrador

El sistema **no incluye usuarios ni contraseñas por defecto**. Elija una opción:

### Opción A — Página de configuración inicial (recomendada)

1. Ejecute la aplicación y abra **http://localhost:5080**.
2. Mientras **no exista ningún usuario**, el inicio de sesión muestra el enlace
   **"Todavía no existe ningún usuario" → Crear el primer administrador**
   (o abra directamente **/Account/ConfiguracionInicial**).
3. Complete el correo y una contraseña (mínimo 8 caracteres, con mayúscula,
   minúscula y número). La página deja de estar disponible en cuanto se crea el
   primer usuario.

### Opción B — Configuración con `dotnet user-secrets` (sin guardar claves en el repo)

```powershell
cd C:\Users\JeffWin\Desktop\TallerMecanicoSantoDomingo
dotnet user-secrets init --project src\TallerMecanicoSantoDomingo
dotnet user-secrets set "AdministradorInicial:Email"    "admin@taller.local"      --project src\TallerMecanicoSantoDomingo
dotnet user-secrets set "AdministradorInicial:Password" "SuClaveSegura123"        --project src\TallerMecanicoSantoDomingo
```

Al arrancar, si no existe ningún usuario y estas claves están configuradas, el
administrador se crea automáticamente.

---

## 6. Funcionalidades incluidas

- **Inicio de sesión** con mostrar/ocultar contraseña, protección antiforgery y
  bloqueo tras 5 intentos fallidos.
- **Panel (Dashboard)** con conteos reales de la base de datos, gráficos
  (Chart.js) y **alertas de carga de trabajadores** (con 2 servicios activos).
- **Clientes**: alta, edición, ficha con sus vehículos e historial de facturas;
  desactivación lógica.
- **Vehículos**: alta, edición, ficha con su **historial completo de servicios**
  (trabajadores, productos y facturas asociadas).
- **Servicios**: registrados por tipo (Mantenimiento / Pintura), con estados
  Abierto / Finalizado / Cancelado, asignación de trabajadores y consumo de
  productos.
- **Trabajadores** y **Asignaciones** (relación N:M servicio–trabajador).
- **Productos (repuestos)** y **Productos utilizados**.
- **Facturación**: asistente que solo muestra servicios elegibles del cliente,
  **cálculo de subtotal, IVA y total en el servidor**, impresión de la factura y
  **anulación** (conserva el histórico).
- **Historial del vehículo** y **Reportes** (servicios, vehículos, por cliente,
  trabajadores, facturas y productos) con **exportación a CSV** e impresión.

---

## 7. Reglas de negocio (implementación)

Las 14 reglas se aplican **en el servidor** (nunca solo en el navegador) y, donde
corresponde, también como restricciones físicas de SQL Server.

| # | Regla | Dónde se implementa |
|---|-------|---------------------|
| 1 | Identificadores únicos (cliente, vehículo, factura) | `ClienteService`, `VehiculoService`, `FacturacionService` + `PRIMARY KEY`/`UNIQUE` en `TallerMecanicoSantoDomingo.sql` |
| 2 | Un vehículo pertenece a un único cliente (1:N) | `VehiculoService.Crear` + clave foránea `Vehiculos → Clientes` |
| 3 | Un vehículo puede tener muchos servicios (historial) | `ServicioService`, `VehiculoService.ObtenerHistorial` + FK `Servicios → Vehiculos` |
| 4 | Tipificación del servicio (Mantenimiento / Pintura) | `ServicioService` + `CHECK` de `TipoServicio` |
| 5 | Compatibilidad área del trabajador ↔ tipo de servicio | `AsignacionService.Asignar` valida la combinación directamente |
| 6 | No repetir la pareja servicio–trabajador | `AsignacionService.Asignar` + índice `UNIQUE` en `DetalleServicio` |
| 7 | Máximo 2 servicios activos por trabajador, con concurrencia segura | `AsignacionService.Asignar` (transacción + `UPDLOCK`/`HOLDLOCK`) |
| 8 | `FechaFin` no anterior a `FechaInicio` | `ServicioService` + `CHECK CK_Servicios_FechaFin` |
| 9 | No repetir un producto dentro del mismo servicio | `ProductoService.AgregarAlServicio` + `UNIQUE` en `ProductosUtilizados` |
| 10 | Cantidades > 0, precios ≥ 0 y dinero en `DECIMAL` (nunca `float`) | `ProductoService` + `CHECK` + columnas `decimal(12,2)` / `decimal(12,3)` |
| 11 | Factura con al menos un detalle y sin doble facturación de un servicio | `FacturacionService.CrearFactura` (el servicio se libera al anular) |
| 12 | Todos los servicios de una factura pertenecen al mismo cliente | `FacturacionService.CrearFactura` |
| 13 | Total = suma de subtotales + IVA configurable (valor inicial 15 %) | `FacturacionService` + `ConfiguracionTaller.PorcentajeIVA` |
| 14 | Sin borrados destructivos: integridad referencial + desactivación lógica | Campos `Activo`, claves foráneas sin cascada y `usp_AnularFactura` |

La **anulación de facturas** se realiza con el procedimiento almacenado
`dbo.usp_AnularFactura`, que conserva el registro y libera los servicios para
volver a facturarlos.

---

## 8. Pruebas

```powershell
# Todas las pruebas (unitarias + integración)
dotnet test TallerMecanicoSantoDomingo.sln

# Solo las unitarias (no necesitan SQL Server)
dotnet test TallerMecanicoSantoDomingo.sln --filter "Categoria!=Integracion"
```

- **Pruebas unitarias** (`PruebasUnitariasReglas.cs`): validan las 14 reglas de
  negocio usando el proveedor **InMemory** de EF Core (rápidas, sin base de datos).
- **Pruebas de integración** (`PruebasIntegracionSqlServer.cs`): se ejecutan
  contra **SQL Server** y comprueban el esquema físico, las restricciones `CHECK`,
  el procedimiento `usp_AnularFactura` y la **Regla 7 con concurrencia real**
  (tres asignaciones simultáneas: solo dos prosperan).

> Las pruebas de integración **requieren** SQL Server con la base
> `TallerMecanicoSantoDomingo` creada (sección 4). Si SQL Server no está
> disponible, se marcan como **omitidas** en lugar de fallar. Los datos
> temporales que crean se eliminan siempre al terminar cada prueba.

---

## 9. Seguridad

- Autenticación mediante **ASP.NET Core Identity**; contraseñas con hash.
- **Antiforgery/CSRF** activo en todas las peticiones POST (formularios y AJAX).
- **Cookies** de autenticación `HttpOnly`, `SameSite=Lax` y `Secure` cuando el
  sitio usa HTTPS.
- **Autorización por omisión**: toda la aplicación exige usuario autenticado
  (política de respaldo); el inicio de sesión es la única excepción.
- Codificación automática de salidas Razor (anti-XSS).
- Validación **en el servidor** de todos los formularios.
- Los mensajes de error **no exponen** detalles internos de SQL Server.
- No hay cadenas de conexión ni contraseñas versionadas: la configuración
  sensible recomendada es `dotnet user-secrets` o variables de entorno.

---

## 9.1. Extensiones técnicas respecto al documento original

El documento "MER y Relacional" define las entidades y la mayoría de los
atributos. Se añadieron, **sin eliminar ni fusionar entidades**, los siguientes
elementos auxiliares, señalados explícitamente como extensiones técnicas:

- Campos de **desactivación lógica y auditoría** (`Activo`, `FechaRegistro`) para
  cumplir la Regla 14 (no borrar registros).
- Tabla catálogo **`Areas`** (`Mantenimiento mecánico`, `Pintura`) que representa
  la especialidad del trabajador mediante clave foránea (3FN).
- Atributos de facturación necesarios para el cálculo real de totales
  (`Subtotal`, `IVA`, `Estado`, `MetodoPago`, `Notas`).

---

## 10. Notas

- La base de datos se llama exactamente **`TallerMecanicoSantoDomingo`**.
- El **esquema SQL es la fuente única de verdad**; `TallerDbContext` lo replica
  y no se usan migraciones de EF Core.
- El porcentaje de IVA es **configurable** (`ConfiguracionTaller:PorcentajeIVA`);
  el documento original no fija una tasa, por lo que el valor inicial es **15 %**.
