# Vertical slices and future events

## Context

Projects y Tasks crecerán con casos de uso independientes; algunos cambios requerirán auditoría y notificaciones.

## Decision

Cada módulo organizará sus casos de uso como vertical slices. En futuras iteraciones se usarán eventos de dominio o aplicación in-process para desacoplar acciones como asignar, bloquear o completar tareas de Activity y Notifications.

## Consequences

No se añade MediatR ni infraestructura de mensajería externa en el bootstrap. El futuro frontend se ubicará en `src/frontend/` y usará Vue 3, Vite, Pinia y Vue Router.
