# Nakama MVP RC1

## MVP feature freeze

Nakama entra en congelamiento funcional para el candidato de lanzamiento MVP. Desde este punto sólo se aceptan correcciones de regresiones reproducibles P0 o P1, de seguridad, de configuración de despliegue o de compilación que impidan el piloto. Las mejoras de producto se registran en el backlog; no se incorporan al RC por conveniencia.

## Included

El alcance incluido es el siguiente:

- Identity/Auth: inicio de sesión JWT con cookie HttpOnly en Production, CSRF para mutaciones, roles Admin y Collaborator, y usuarios activos.
- Projects: creación y administración de proyectos, miembros y acceso por pertenencia.
- Stages: creación, orden y administración de etapas de proyecto.
- Tasks: creación, edición, asignación, prioridad, fechas y estados.
- Workflow: iniciar, enviar a revisión, solicitar cambios, completar y cancelar tareas conforme a las transiciones permitidas.
- Subtasks, dependencies y blockers por tarea.
- Activity: registro de acciones de trabajo relevantes.
- My Work: tareas asignadas del colaborador.
- Comments y attachments con acceso autenticado por proyecto.
- Notifications para asignaciones y transiciones relevantes.
- Dashboard administrativo de seguimiento operativo.

## User roles

| Rol | Capacidades del RC |
| --- | --- |
| Admin | Accede al Dashboard y administra usuarios, proyectos, miembros, etapas, tareas y asignaciones. |
| Collaborator | Accede sólo a proyectos donde es miembro, trabaja sus tareas asignadas, consulta My Work y participa en comentarios, adjuntos y workflow dentro de esos proyectos. |
| Non-member | No puede consultar tareas ni colaborar mediante comentarios o adjuntos de un proyecto al que no pertenece. |

## Core workflow

1. Un Admin crea el proyecto, sus etapas, miembros y tareas, y asigna responsables.
2. El Collaborator consulta My Work y la notificación de asignación, inicia la tarea y registra subtasks, blockers, comentarios o adjuntos según corresponda.
3. El Collaborator envía la tarea a revisión. El Admin puede solicitar cambios o completarla; las transiciones dejan Activity y notificaciones aplicables.

## Operational requirements

- Production requiere PostgreSQL externo, una clave JWT externa de al menos 32 bytes, orígenes CORS HTTPS explícitos, `AllowedHosts` restringido al hostname de la API, rate limiting de login y una ruta absoluta y persistente para adjuntos.
- El frontend de Production exige `VITE_API_BASE_URL` como URL HTTPS absoluta de la API; el build rechaza HTTP, credenciales, query y fragment.
- Las migraciones se aplican de forma operativa antes del despliegue; la API no actualiza el esquema al arrancar.
- Consulte [despliegue](../operations/deployment.md), [backup y restore](../operations/backup-restore.md), [smoke test](../operations/pilot-smoke-test.md) y [datos UAT](../operations/pilot-test-data.md).

## Known limitations

| Prioridad | Estado | Limitación o deuda confirmada |
| --- | --- | --- |
| P1 | Ninguna abierta | La revisión RC no mantiene hallazgos P1 abiertos. |
| P2 | Backlog | My Work consume la vista actual sin exponer controles de filtros ni paginación/cursor en la interfaz. |
| P2 | Backlog | Los adjuntos no tienen integración con un motor antimalware externo; el tipo, extensión y tamaño sí se validan. Debe resolverse antes de aceptar archivos de fuentes no confiables. |
| P2 | Operación | La suite backend completa puede superar una ventana corta de ejecución interactiva; la regresión RC se realiza por grupos focalizados y no indica un defecto funcional. |
| P3 | Backlog | El tablero no ofrece drag and drop para mover tareas. |
| P3 | Backlog | La interfaz no ofrece edición inline de tareas. |

No hay P0 abiertos. Estas entradas son deuda o trabajo diferido confirmado, no regresiones introducidas por este RC.

## Pilot status

La evidencia de piloto del 2026-09-03 registra inicio Development con `GET /health` en `200 Healthy`; flujo Admin y Collaborator; y denegación `403` para un non-member al acceder a tarea, comentarios, adjuntos y descarga. El helper `PilotDataSeeder` es exclusivo de Development, está deshabilitado por defecto, exige opt-in y passwords externos cuando se habilita, no se ejecuta en Production y es idempotente para el dataset documentado. Consulte el [registro de hallazgos](../operations/pilot-findings.md): no hay P0/P1 abiertos.

La validación final del 2026-09-06 pasó compilación backend Release sin advertencias ni errores, publicación Release verificable, 82 pruebas backend focalizadas, 45 pruebas frontend, build frontend y la comprobación de migraciones sin cambios pendientes. El hardening incluye autorización Admin consultada contra el rol actual, revocación efectiva tras cambio de rol, límites de login incluso ante el primer acceso concurrente, JWT mínimo de 32 bytes, CORS HTTPS fuera de Development, `AllowedHosts` explícito y comprobado contra host no permitido, sesión browser con cookie HttpOnly/CSRF y cabeceras API de no caché, anti-framing, no-sniff, referrer restrictivo y HSTS. El logout elimina la cookie de sesión del navegador. Los Admin activos también conservan de forma consistente el acceso administrativo a comentarios y adjuntos aunque no sean miembros del proyecto. La operación dispone de `health/live` y `health/ready` separados, conservando `/health`. El arranque Development de la validación mantuvo `PilotData` deshabilitado y no reinicializó datos.

## Deferred features

Quedan fuera del MVP, como backlog y no como defectos: Search, Reporting, Workspaces/organizations, email notifications, reminders, realtime y exports. Cualquier incorporación de esta lista requiere una nueva planificación de release y no forma parte de la estabilización de RC1.
