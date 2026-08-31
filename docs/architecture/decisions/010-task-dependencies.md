# ADR 010: Dependencias estructurales de tareas

## Decisión

`TaskDependency` representa un prerrequisito estructural entre dos tareas del mismo proyecto. Una dependencia se satisface exclusivamente cuando la tarea prerrequisito está en estado `Completed`; `Cancelled` y los demás estados permanecen pendientes.

Antes de crear una arista se cargan todas las aristas de tareas del proyecto y se construye un mapa de adyacencia. Una búsqueda en profundidad desde el prerrequisito propuesto hasta la tarea origen rechaza ciclos directos e indirectos de cualquier profundidad, pero permite grafos acíclicos dirigidos.

## Consecuencias

Las operaciones `start`, `submit-review` y `complete` rechazan tareas con prerrequisitos pendientes. Las dependencias no cambian estados automáticamente y son distintas de los blockers operativos: un blocker registra un impedimento temporal, mientras que una dependencia fija un requisito estructural.

El detalle informa `dependencyProgress` y `dependenciesSatisfied`; el listado añade `pendingDependencyCount`. Estos valores se calculan en PostgreSQL mediante una agregación agrupada por tarea, junto con los otros lotes de lectura, por lo que no hay consultas adicionales por tarea.
