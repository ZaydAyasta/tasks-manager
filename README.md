# Nakama

Base técnica del sistema interno de gestión de proyectos y tareas.

## Frontend Vue

El cliente web está en `src/frontend/` y usa Vue 3, Vite, Vue Router y Pinia.

```powershell
cd src/frontend
Copy-Item .env.example .env
npm install
npm run dev
```

`VITE_API_BASE_URL` configura la URL de la API; por defecto el ejemplo apunta a `http://localhost:5000`.
Inicie antes la API con las credenciales JWT y PostgreSQL indicadas más abajo. En desarrollo, la API acepta por CORS `http://localhost:5173`; puede modificar `Cors:AllowedOrigins` si usa otro origen.

El login consume `POST /api/auth/login` y la restauración de sesión verifica siempre `GET /api/auth/me`. El access token se conserva temporalmente en `localStorage` porque esta iteración no incluye refresh tokens ni cookies HttpOnly.

Los Admin pueden configurar el trabajo desde **Proyectos**: crear un proyecto, definir sus etapas y equipo, y luego crear/organizar las tareas desde el tablero. Todas las mutaciones vuelven a consultar la API para respetar la concurrencia del backend.

Los Collaborator trabajan desde **Mi trabajo**, que consume `GET /api/me/work`. El endpoint usa el usuario del JWT, devuelve sólo sus tareas no terminales por defecto y entrega una página de resúmenes con proyecto, etapa, progreso, dependencias y bloqueos. No necesita ni acepta un `userId`.

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

Use `POST /api/auth/login` with `{ "email": "...", "password": "..." }`; successful login returns an access token. Send it as `Authorization: Bearer <accessToken>` and use `GET /api/auth/me` to read the current user. User creation requires an Admin token and a password of at least eight characters. Existing users from before this migration have no usable password hash and must be provisioned/reset through an approved administrative process before they can log in.

Las pruebas de integración usan exclusivamente `ConnectionStrings:NakamaTestDatabase`. La base debe ser dedicada: la suite la elimina y recrea antes de cada escenario, por lo que el usuario PostgreSQL necesita permisos para crear y eliminar esa base. No configure esa cadena con una base de desarrollo.

`POST /api/projects` recibe temporalmente `createdByUserId` hasta que exista autenticación. La migración `AddProjects` crea proyectos y membresías; aplíquela con el mismo comando `dotnet ef database update` después de configurar la conexión de desarrollo.

La migración `AddProjectStages` incorpora etapas ordenadas y sus membresías. La suite de integración reutiliza una única colección PostgreSQL no paralela para reinicializar `nakama_test` de forma segura.

La migración `AddTasks` incorpora el núcleo de tareas y asignados. `createdByUserId` seguirá siendo temporal hasta implementar autenticación.

La migración `AddTaskBlockers` incorpora blockers con historial, categorías fijas y restauración automática del estado previo al resolver el último bloqueo activo. Los IDs de reportante y resolvedor son temporales hasta implementar autenticación.

La migración `AddTaskDependencies` incorpora prerequisitos estructurales entre tareas. Una dependencia solo queda satisfecha cuando su prerrequisito está `Completed`; los blockers siguen siendo un mecanismo operativo independiente.

La migración `AddActivityLog` incorpora el histórico inmutable de Projects y Tasks. El log es auditoría, no event sourcing: el estado sigue viviendo en las entidades de negocio. Los feeds están disponibles en `/api/projects/{projectId}/activity` y `/api/tasks/{taskId}/activity`.

La migración `AddSubtasks` incorpora checklist ordenado por tarea. Las subtareas se eliminan físicamente, se protegen con `taskVersion` y son de solo lectura durante `InReview`, `Completed` y `Cancelled`.

`GET /health` siempre comprueba que la aplicación está disponible. Cuando se configura la cadena, también comprueba PostgreSQL. En Development, el documento OpenAPI queda disponible en `/openapi/v1.json`.

El futuro frontend se ubicará en `src/frontend/` y utilizará Vue 3, Vite, Pinia y Vue Router.

La migración `AddTaskCollaboration` añade comentarios y metadata de adjuntos. En Development, configure `Attachments:StoragePath` (por defecto `App_Data/attachments`, relativo a la API) y `Attachments:MaxFileSizeBytes` (por defecto 10 MB). La carpeta se crea al primer upload, no se sirve públicamente y las descargas autorizadas pasan por la API.
