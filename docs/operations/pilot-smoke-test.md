# Smoke test del piloto interno

Ejecute este checklist después del despliegue, migraciones y backup verificado. Use cuentas de prueba del piloto y un archivo permitido pequeño. Registre `traceId` si aparece un error.

## Flujo funcional

### Admin

1. Inicie sesión.
2. Abra Dashboard y confirme que carga.
3. Cree un proyecto.
4. Cree etapas.
5. Agregue un Collaborator al proyecto.
6. Cree una tarea.
7. Asigne la tarea al Collaborator.
8. Confirme que el Collaborator recibe la notificación.

### Collaborator

9. Inicie sesión.
10. Abra **Mi trabajo** y encuentre la tarea asignada.
11. Abra el detalle de la tarea.
12. Iníciela.
13. Cree una subtarea.
14. Cree un comentario.
15. Cargue un adjunto permitido.
16. Reporte un blocker.
17. Resuelva el blocker.
18. Envíe la tarea a revisión.

### Admin y cierre

19. Confirme la notificación de revisión.
20. Solicite cambios.
21. Confirme que el Collaborator recibe esa notificación.
22. Como Collaborator, reenvíe la tarea a revisión.
23. Como Admin, complete la tarea.
24. Verifique ActivityLog.
25. Confirme que Dashboard se actualizó.
26. Marque y desmarque estado leído/no leído de notificaciones según el flujo disponible.
27. Descargue el adjunto autenticado.
28. Compruebe `GET /health/live` y `GET /health/ready`.
29. Compruebe las restricciones de permisos indicadas abajo.

## Smoke test de seguridad

Con una cuenta de prueba, confirme en Production que el login crea la cookie `nakama.access-token` con `HttpOnly`, `Secure` y `SameSite=None`, y que el cuerpo de la respuesta no contiene un access token. Use la SPA para crear el proyecto de prueba y confirme que la mutación normal funciona; una repetición autenticada sin `X-Nakama-Csrf` debe devolver `403`.

Con un Admin temporal, inicie sesión, cámbiele el rol a Collaborator desde otra sesión Admin y confirme que la sesión anterior recibe `403` al abrir `/api/admin/dashboard`. No reutilice una cuenta de administración real para esta prueba.

Con un Collaborator autenticado, confirme que recibe `403` al intentar:

- Abrir `/api/admin/dashboard`.
- Crear un proyecto.
- Ejecutar gestión estructural de proyecto, etapas o miembros.

Con un usuario que no pertenece al proyecto, confirme el rechazo al intentar:

- Abrir una tarea del proyecto.
- Listar, crear o editar comentarios de esa tarea.
- Listar, cargar, descargar o borrar adjuntos de esa tarea.

No use estos intentos para modificar datos reales del piloto. Haga el smoke test sobre el proyecto de prueba creado en este procedimiento y archive o elimine sus datos conforme a la política operativa.

## Resultado

El piloto queda aprobado sólo si el flujo funcional, descarga de adjuntos, health check y rechazos de permisos se completan. Si hay un fallo, conserve hora, usuario, URL, `traceId` y logs relacionados antes de reintentar.
