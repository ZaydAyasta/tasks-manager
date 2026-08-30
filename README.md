# Nakama

Base técnica del sistema interno de gestión de proyectos y tareas.

## Configuración local

La API lee PostgreSQL desde `ConnectionStrings:NakamaDatabase`. Para desarrollo local:

```powershell
dotnet user-secrets set "ConnectionStrings:NakamaDatabase" "Host=localhost;Port=5432;Database=nakama;Username=postgres;Password=<secret>" --project src/backend/Nakama.Api
dotnet user-secrets set "ConnectionStrings:NakamaTestDatabase" "Host=localhost;Port=5432;Database=nakama_test;Username=postgres;Password=<secret>" --project src/backend/Nakama.Api
dotnet ef database update --project src/backend/Nakama.Api --startup-project src/backend/Nakama.Api
dotnet run --project src/backend/Nakama.Api
```

Como alternativa, configure la variable de entorno `ConnectionStrings__NakamaDatabase`.

Las pruebas de integración usan exclusivamente `ConnectionStrings:NakamaTestDatabase`. La base debe ser dedicada: la suite la elimina y recrea antes de cada escenario, por lo que el usuario PostgreSQL necesita permisos para crear y eliminar esa base. No configure esa cadena con una base de desarrollo.

`POST /api/projects` recibe temporalmente `createdByUserId` hasta que exista autenticación. La migración `AddProjects` crea proyectos y membresías; aplíquela con el mismo comando `dotnet ef database update` después de configurar la conexión de desarrollo.

La migración `AddProjectStages` incorpora etapas ordenadas y sus membresías. La suite de integración reutiliza una única colección PostgreSQL no paralela para reinicializar `nakama_test` de forma segura.

La migración `AddTasks` incorpora el núcleo de tareas y asignados. `createdByUserId` seguirá siendo temporal hasta implementar autenticación.

La migración `AddTaskBlockers` incorpora blockers con historial, categorías fijas y restauración automática del estado previo al resolver el último bloqueo activo. Los IDs de reportante y resolvedor son temporales hasta implementar autenticación.

La migración `AddSubtasks` incorpora checklist ordenado por tarea. Las subtareas se eliminan físicamente, se protegen con `taskVersion` y son de solo lectura durante `InReview`, `Completed` y `Cancelled`.

`GET /health` siempre comprueba que la aplicación está disponible. Cuando se configura la cadena, también comprueba PostgreSQL. En Development, el documento OpenAPI queda disponible en `/openapi/v1.json`.

El futuro frontend se ubicará en `src/frontend/` y utilizará Vue 3, Vite, Pinia y Vue Router.
