# ADR 011: ActivityLog histórico

`ActivityLog` es un registro persistente e inmutable de actividades humanas de Projects y Tasks. Registra lifecycle de proyecto, membresías, etapas, tareas, asignaciones, workflow, blockers, subtareas y dependencias. No es event sourcing ni reconstruye el estado: las entidades de negocio siguen siendo la fuente de verdad.

Cada actividad comparte el `NakamaDbContext` y el `SaveChangesAsync` de la mutación que describe, por lo que no puede persistirse una actividad sin el cambio asociado. Los feeds paginados de proyecto y tarea se ordenan de forma estable por fecha e identificador.

Mientras no exista autenticación, los comandos reutilizan el actor semántico ya disponible o reciben temporalmente `actorUserId`; en el futuro ese valor saldrá del contexto autenticado. `null` se reserva para automatizaciones reales. Metadata `jsonb` es deliberadamente pequeña (IDs y valores útiles para presentación), y los feeds paginados de Project/Task serán la fuente para métricas futuras, sin calcularlas todavía.
