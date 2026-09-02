# 013 — Colaboración dentro de tareas

Las notas de una tarea se almacenan como comentarios simples, con la persona autora derivada exclusivamente del JWT. La edición es solo de la persona autora; la eliminación puede hacerla la autora o un Admin que ya pertenece al proyecto.

Los adjuntos guardan únicamente metadata en PostgreSQL. Los bytes se almacenan mediante `IAttachmentStorage`; la implementación inicial usa filesystem local configurado en `Attachments:StoragePath`. Los nombres físicos son GUIDs seguros, nunca el nombre original, y la carpeta no se publica como static files: toda descarga pasa por un endpoint con autenticación y pertenencia al proyecto.

El MVP permite PDF, PNG, JPEG, TXT, DOCX y XLSX hasta el límite configurable. Upload guarda el archivo y luego la metadata; si falla la persistencia intenta eliminar el archivo. Delete elimina primero el archivo y luego la metadata, por lo que no pretende una transacción distribuida y puede requerir reparación operativa si falla el segundo paso. Producción deberá migrar esta abstracción a object storage y añadir scanning externo.

Comments y attachments generan ActivityLog sin guardar contenido de comentarios ni rutas físicas. No se implementan notificaciones, menciones, hilos, rich text ni realtime.
