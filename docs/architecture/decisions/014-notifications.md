# 014 — Notificaciones dentro de la aplicación

## Contexto

Nakama necesita avisar a las personas afectadas por cambios de trabajo sin crear un canal externo ni exponer información de otros usuarios. El Dashboard Admin es estrictamente de lectura y no debe producir estos avisos.

## Decisión

Las notificaciones se almacenan como registros propios del receptor y se consultan exclusivamente desde rutas `/api/me`. El actor, destinatario y acceso se derivan del token: ninguna ruta acepta un identificador de usuario para impersonar a otra persona.

El escritor filtra destinatarios vacíos, repetidos y al actor de la mutación. Antes de insertar respeta las preferencias del tipo de notificación. El centro de notificaciones ofrece feed, contador de no leídas, marcado individual o masivo y preferencias; la interfaz conserva el contador en el layout y navega a la tarea o proyecto relacionado.

## Consecuencias

- Las notificaciones son in-app; no se envían correos ni recordatorios en esta iteración.
- Cada operación conserva el aislamiento por receptor y la lectura/marcado no expone filas ajenas.
- `GET /api/admin/dashboard` no escribe ActivityLog ni Notifications; es sólo un read-model agregado.
- Las pruebas PostgreSQL cubren creación, exclusión del actor, preferencias, lectura, conteo y aislamiento. Las pruebas frontend cubren servicio, presentación, estado y contador.
