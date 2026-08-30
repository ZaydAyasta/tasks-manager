# Identity user model

## Context

Nakama necesita usuarios antes de crear proyectos, asignaciones y tareas.

## Decision

`User` pertenece al módulo Identity y tiene un único rol global: `Admin` o `Collaborator`. Un Admin puede participar operativamente y recibir tareas. La autenticación se implementará en una iteración posterior.

## Consequences

Projects y Tasks podrán referenciar un `UserId` real. Todavía no existen controles de autorización; los endpoints mutantes deberán restringirse a Admin cuando haya autenticación.
