# Datos reproducibles para piloto UAT

## Alcance y seguridad

Este helper existe sólo para preparar un entorno **Development** del piloto. No es una migración, no forma parte del despliegue y no ejecuta nada en Production: `PilotDataSeeder` retorna antes de leer la configuración de piloto o resolver la base de datos fuera de `Development`.

El helper está deshabilitado por defecto. No hay passwords, tokens ni connection strings en este documento, en `appsettings` ni en el repositorio. Provea los passwords temporalmente mediante user-secrets o variables de entorno seguras. Al reejecutarlo no cambia el password de una cuenta que ya exista; para reinicializar credenciales, use una base Development aislada y vuelva a poblarla.

## Identidades del piloto

| Rol | Nombre | Email de Development | Membresía del proyecto |
| --- | --- | --- | --- |
| Admin | Ana Torres | `ana.torres@pilot.local` | Owner |
| Collaborator | Diego Ramos | `diego.ramos@pilot.local` | Member |
| Non-member | Lucía Vargas | `lucia.vargas@pilot.local` | Ninguna |

Los emails son identificadores locales de UAT; no se usan fuera de Development.

## Preparación explícita

Antes de ejecutar, confirme que la configuración Development normal (PostgreSQL, JWT, CORS y storage de adjuntos) es válida. Configure valores secretos fuera del repositorio y arranque la API:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:DevelopmentBootstrap__PilotData__Enabled = "true"
$env:DevelopmentBootstrap__PilotData__AdminPassword = "<password-seguro-de-Ana>"
$env:DevelopmentBootstrap__PilotData__CollaboratorPassword = "<password-seguro-de-Diego>"
$env:DevelopmentBootstrap__PilotData__NonMemberPassword = "<password-seguro-de-Lucía>"
dotnet run --project src/backend/Nakama.Api
```

Si ya existe un proyecto activo con el mismo nombre y se confirmó que es el proyecto de UAT previsto, agregue explícitamente:

```powershell
$env:DevelopmentBootstrap__PilotData__AdoptExistingProject = "true"
```

Sin esa aprobación, el helper se detiene sin escribir datos para evitar adoptar un proyecto local ajeno. Nunca active estas variables en Production.

## Dataset creado

Proyecto: **Implementación Portal de Clientes**

Etapas, en este orden: **Análisis**, **Diseño**, **Desarrollo**, **Pruebas** y **Entrega**.

Las cinco tareas quedan asignadas inicialmente a Diego y en estado `Pending`:

| Tarea | Etapa | Objetivo de UAT |
| --- | --- | --- |
| Validar flujo colaborativo | Análisis | Start, subtasks, blocker, comment, attachment, submit review y cierre. |
| Revisar dependencia funcional | Diseño | Depende de `Validar flujo colaborativo`; comprueba bloqueo y liberación de dependencia. |
| Implementar permisos de proyecto | Desarrollo | Assignment, My Work y acceso de miembro. |
| Ejecutar pruebas de aceptación | Pruebas | Dashboard y seguimiento de progreso. |
| Preparar entrega del piloto | Entrega | Vista de proyecto y cierre. |

El helper crea una notificación `TaskAssigned` para Diego sobre la primera tarea. No crea subtasks, blockers, comments ni adjuntos: esas acciones quedan deliberadamente para la validación por las APIs y la interfaz reales. Una segunda ejecución conserva acciones UAT ya realizadas y no duplica usuarios, etapas, tareas, assignment, dependency ni la notificación inicial.

## Checklist UAT

Ejecute el [smoke test del piloto](pilot-smoke-test.md) sobre este dataset. Su recorrido cubre: Admin (login, Dashboard, proyecto, etapas, miembros, tareas y assignment); Collaborator (My Work, notificación, start, subtasks, blocker, comment, attachment y submit review); cierre Admin (notificación, request changes y complete); y denegaciones de acceso para Lucía como non-member. No reinicialice el dataset entre esos pasos: la secuencia está diseñada para conservar evidencia de ActivityLog, notificaciones, dependency y adjunto autenticado.

## Evidencia de ejecución local

El 2026-09-03 se verificó un arranque limpio de Development y `GET /health` respondió `200 Healthy`. Con el opt-in de adopción para el proyecto local existente, el helper creó/validó las tres identidades, la membresía de Diego, las cinco tareas, su dependency y la notificación inicial.

La validación HTTP posterior confirmó: Admin Dashboard `200`; Diego My Work con 5 ítems y notificación no leída; start `204`; subtask `201`; blocker `201` y resolución `200`; comment `201`; submit review/request changes/re-submit/complete `204`; upload/download de adjunto `201/200`; y `403` para Lucía al leer tarea, comments, attachments y download. No se mostraron ni conservaron credenciales en el registro.
