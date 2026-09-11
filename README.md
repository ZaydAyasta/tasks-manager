# Nakama

Nakama es un sistema interno para organizar proyectos, etapas y tareas de un equipo en un solo lugar. Está pensado para que las personas administradoras configuren el trabajo y tengan visibilidad operativa, mientras que los colaboradores se concentran en las tareas que les han sido asignadas.

## ¿Qué problema resuelve?

Centraliza el ciclo de trabajo de un proyecto: desde crear su estructura y asignar responsables hasta controlar bloqueos, revisiones, evidencias y cierre. Así se evita depender de conversaciones dispersas para saber quién hace qué, en qué etapa está una tarea o qué necesita atención.

## Funcionalidades principales

- Proyectos con etapas ordenadas, miembros y estados de ciclo de vida.
- Tareas con prioridad, vencimiento, responsables, subtareas y control de versión para cambios concurrentes.
- Flujo de trabajo: pendiente, en progreso, bloqueada, en revisión y completada.
- Dependencias entre tareas y bloqueos que restauran el estado anterior cuando se resuelven.
- Comentarios, adjuntos privados y registro de actividad inmutable por proyecto y tarea.
- Notificaciones dentro de la aplicación y preferencias por tipo de evento.
- Dashboard administrativo para identificar proyectos, tareas prioritarias, bloqueos y trabajo en revisión.
- Vista **Mi trabajo** para que cada colaborador vea sólo sus tareas activas.

## Roles

| Rol | Qué puede hacer |
| --- | --- |
| **Admin** | Crear y administrar proyectos, etapas, equipo y tareas; asignar responsables; revisar, solicitar cambios y completar tareas; consultar el Dashboard. |
| **Collaborator** | Consultar sus proyectos y tareas asignadas; iniciar trabajo, gestionar subtareas, comentar, adjuntar archivos, reportar o resolver bloqueos y enviar tareas a revisión. |

Las autorizaciones se validan también en la API: un usuario no puede actuar como otra persona ni acceder a proyectos de los que no forma parte.

## Recorrido habitual

1. Un Admin crea el proyecto, define las etapas y agrega al equipo.
2. El Admin crea tareas y asigna responsables.
3. El Collaborator trabaja desde **Mi trabajo**, registra avances y envía la tarea a revisión.
4. El Admin aprueba la tarea o solicita cambios.
5. El historial y las notificaciones conservan la trazabilidad del proceso.

## Arquitectura

| Componente | Tecnología | Responsabilidad |
| --- | --- | --- |
| Cliente web | Vue 3, Vite, Vue Router y Pinia | Interfaz, sesión y flujos de trabajo. |
| API | ASP.NET Core / .NET 10 | Reglas de negocio, autorización, concurrencia y endpoints. |
| Persistencia | PostgreSQL con EF Core | Proyectos, tareas, usuarios, auditoría y notificaciones. |
| Seguridad | JWT, cookie HttpOnly en Production y CSRF | Protección de sesión y mutaciones autenticadas. |

## Inicio rápido local

1. Configura PostgreSQL, JWT y el primer administrador siguiendo [Configuración local](#configuración-local) y [Autenticación en Development](#authentication-development-setup).
2. Inicia la API en `http://localhost:5000`:

   ```powershell
   dotnet run --project src/backend/Nakama.Api
   ```

3. En otra terminal, inicia el cliente web:

   ```powershell
   cd src/frontend
   Copy-Item .env.example .env
   npm install
   npm run dev
   ```

4. Abre `http://localhost:5173` e ingresa con una cuenta de Development.

Para datos de demostración reproducibles y un recorrido completo de aceptación, consulta [datos UAT](docs/operations/pilot-test-data.md) y el [smoke test del piloto](docs/operations/pilot-smoke-test.md).

## Operaciones

Para el piloto interno consulte [despliegue](docs/operations/deployment.md), el [despliegue genérico con contenedores](docs/operations/container-deployment.md), [backup y restore](docs/operations/backup-restore.md), el [smoke test](docs/operations/pilot-smoke-test.md), los [datos UAT](docs/operations/pilot-test-data.md), el [registro de hallazgos](docs/operations/pilot-findings.md) y las [notas MVP RC1](docs/releases/mvp-rc1.md). Production requiere configuración externa para PostgreSQL, JWT, CORS y almacenamiento persistente de adjuntos.

En una instalación Production nueva, aplique primero las migraciones y luego cree el único primer Admin de forma explícita con `dotnet run --project src/backend/Nakama.Provisioning -- create-first-admin --name "Administrador" --email "admin@nakama.local"`. El comando imprime una contraseña temporal una sola vez; guárdela en un gestor de contraseñas. No configure `DevelopmentBootstrap` para Production.

## Frontend Vue

El cliente web está en `src/frontend/` y usa Vue 3, Vite, Vue Router y Pinia.
Para instalación y build reproducibles use Node 24.15.0 (definido en `src/frontend/.nvmrc`); el rango compatible se declara en `src/frontend/package.json`.

```powershell
cd src/frontend
Copy-Item .env.example .env
npm install
npm run dev
```

`VITE_API_BASE_URL` configura la URL de la API; el ejemplo de localhost es exclusivamente para Development. Todo build de Production debe definir la URL HTTPS desplegada.
Inicie antes la API con las credenciales JWT y PostgreSQL indicadas más abajo. En desarrollo, la API acepta por CORS `http://localhost:5173`; puede modificar `Cors:AllowedOrigins` si usa otro origen.

El login consume `POST /api/auth/login` y la restauración de sesión verifica siempre `GET /api/auth/me`. En Production la API entrega la sesión mediante una cookie `HttpOnly`; el frontend no persiste el access token. Las mutaciones autenticadas incluyen un token CSRF ligado a esa sesión.

Los Admin pueden configurar el trabajo desde **Proyectos**: crear un proyecto, definir sus etapas y equipo, y luego crear/organizar las tareas desde el tablero. Todas las mutaciones vuelven a consultar la API para respetar la concurrencia del backend.

Los Collaborator trabajan desde **Mi trabajo**, que consume `GET /api/me/work`. El endpoint usa el usuario del JWT, devuelve sólo sus tareas no terminales por defecto y entrega una página de resúmenes con proyecto, etapa, progreso, dependencias y bloqueos. No necesita ni acepta un `userId`.

Los Admin inician en **Dashboard** (`/dashboard`). La vista consume exclusivamente `GET /api/admin/dashboard`: un read-model Admin-only que devuelve el resumen de tareas no terminales, progreso por proyecto y hasta 15 tareas priorizadas para atención. El endpoint usa `IClock` para vencimientos, no produce ActivityLog ni Notifications, y ejecuta consultas proyectadas y agrupadas más un único lote de responsables; nunca consulta por proyecto o tarea dentro de un bucle. `activeProjects` cuenta sólo proyectos `Active`; la lista conserva el estado de todos los proyectos y el progreso es `Completed / Total` de tareas, sin subtareas ni porcentajes persistidos.

Estructura principal del frontend:

- `src/app`: router, store de autenticación, cliente API y layout.
- `src/modules`: pantallas de autenticación, proyectos, tareas y Mi trabajo.
- `src/shared`: componentes de estado, tarjetas, badges y estilos base.

## Configuración local

La API lee PostgreSQL desde `ConnectionStrings:NakamaDatabase`. Para desarrollo local:

```powershell
dotnet user-secrets set "ConnectionStrings:NakamaDatabase" "Host=localhost;Port=5432;Database=nakama;Username=postgres;Password=<secret>" --project src/backend/Nakama.Api
dotnet user-secrets set "ConnectionStrings:NakamaTestDatabase" "Host=localhost;Port=5432;Database=nakama_test;Username=postgres;Password=<secret>" --project src/backend/Nakama.Api
dotnet ef database update --project src/backend/Nakama.Api --startup-project src/backend/Nakama.Api
dotnet run --project src/backend/Nakama.Api
```

Como alternativa, configure la variable de entorno `ConnectionStrings__NakamaDatabase`.

## Authentication development setup

JWT uses `Authentication:Jwt:Issuer`, `Audience` and `AccessTokenMinutes` from `appsettings.json`. Set the signing key outside the repository before starting the API:

```powershell
dotnet user-secrets set "Authentication:Jwt:SigningKey" "<long-random-secret>" --project src/backend/Nakama.Api
```

For a fresh development database, configure the first administrator in the local secret store before the first start:

```powershell
dotnet user-secrets set "DevelopmentBootstrap:Admin:Email" "admin@nakama.local" --project src/backend/Nakama.Api
dotnet user-secrets set "DevelopmentBootstrap:Admin:FullName" "Development Administrator" --project src/backend/Nakama.Api
dotnet user-secrets set "DevelopmentBootstrap:Admin:Password" "<password-with-at-least-8-characters>" --project src/backend/Nakama.Api
dotnet ef database update --project src/backend/Nakama.Api --startup-project src/backend/Nakama.Api
dotnet run --project src/backend/Nakama.Api
```

`dotnet run` uses the Development launch profile and listens on `http://localhost:5000`, matching the frontend example configuration. The bootstrap creates an admin only when the database has none, and is disabled outside Development.

Use `POST /api/auth/login` with `{ "email": "...", "password": "..." }` and `GET /api/auth/me` to restore the authenticated session. In Production the browser receives an HttpOnly cookie and must send the `X-Nakama-Csrf` value returned by login or `/me` for authenticated mutations. User creation requires an Admin session and a password of at least eight characters.

Las pruebas de integración usan exclusivamente `ConnectionStrings:NakamaTestDatabase`. La base debe ser dedicada: la suite la elimina y recrea antes de cada escenario, por lo que el usuario PostgreSQL necesita permisos para crear y eliminar esa base. No configure esa cadena con una base de desarrollo.

Los actores de Projects, Tasks, Blockers, Subtasks, Dependencies, Comments y Activity se toman exclusivamente de la sesión autenticada; las solicitudes no aceptan `createdByUserId`, reporter IDs ni actor IDs. Los cambios concurrentes se protegen con `Version` y devuelven `409` cuando corresponde.

Las dependencias sólo se satisfacen cuando su prerrequisito está `Completed`; los blockers son independientes y restauran el estado anterior al resolver el último bloqueo. Activity es auditoría inmutable, no event sourcing; sus feeds están disponibles en `/api/projects/{projectId}/activity` y `/api/tasks/{taskId}/activity`.

`GET /health/live` comprueba sólo que el proceso está disponible. `GET /health/ready` comprueba además PostgreSQL; `GET /health` conserva ese comportamiento de readiness por compatibilidad. En Development, el documento OpenAPI queda disponible en `/openapi/v1.json`.

En Development, configure `Attachments:StoragePath` (por defecto `App_Data/attachments`, relativo a la API) y `Attachments:MaxFileSizeBytes` (por defecto 10 MB). La carpeta se crea al primer upload, no se sirve públicamente y las descargas autorizadas pasan por la API.
