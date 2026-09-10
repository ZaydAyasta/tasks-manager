# Backup y restore operativo

## Qué debe respaldarse

Un backup completo de Nakama requiere dos conjuntos coherentes:

1. PostgreSQL: usuarios, proyectos, tareas, actividad, metadata de adjuntos y notificaciones.
2. Los bytes de adjuntos: el contenido de `Attachments:StoragePath` para `Provider=Local`, o los objetos del bucket para `Provider=S3`.

PostgreSQL no contiene los bytes de adjuntos. Coordine ambos respaldos aproximadamente en el mismo punto temporal y registre la fecha/hora, versión de aplicación y ubicación de storage.

## Backup PostgreSQL

Use una cuenta de PostgreSQL con permisos de lectura de la base objetivo y entregue la contraseña mediante el mecanismo seguro del host, nunca en el comando ni el repositorio.

```powershell
pg_dump --format=custom --file "D:\NakamaBackups\nakama-YYYYMMDD-HHMM.dump" --dbname "<production-connection-string>"
```

El formato `custom` permite inspección y restore selectivo con `pg_restore`. Para `Provider=Local`, respalde la carpeta configurada en `Attachments:StoragePath` con la herramienta aprobada por infraestructura, preservando permisos y estructura de archivos. En contenedores, haga copia o snapshot del volumen persistente montado en `/data/attachments`; no respalde la capa efímera de la imagen.

Para `Provider=S3` (por ejemplo R2), haga una copia del bucket mediante una herramienta compatible con S3, usando credenciales sólo de backup y un destino aislado. La copia debe preservar las keys físicas de Nakama. No restaure objetos sobre el bucket de Production: cree o elija primero un bucket aislado y configure la instancia de restore para usarlo.

## Restore verificable en una base aislada

No ejecute este procedimiento sobre `nakama_dev`, `nakama_test` ni la base de Production.

1. Cree una base de prueba vacía con un nombre inequívoco, por ejemplo `nakama_restore_verify`.
2. Restaure el dump:

   ```powershell
   pg_restore --clean --if-exists --no-owner --dbname "<restore-connection-string>" "D:\NakamaBackups\nakama-YYYYMMDD-HHMM.dump"
   ```

   Si el backup se tomó en formato SQL plano, use `psql --dbname "<restore-connection-string>" --file "<backup.sql>"` en su lugar.
3. Restaure los archivos al directorio persistente aislado o al bucket S3 aislado configurado para el restore. No reutilice el storage de Production.
4. Arranque Nakama con `ASPNETCORE_ENVIRONMENT=Production`, la cadena de restore, una clave JWT de restore y la configuración de attachments aislada.
5. Inicie sesión con una cuenta existente del backup.
6. Abra un proyecto y luego una tarea que tenga adjunto.
7. Descargue el adjunto y compruebe tamaño y contenido esperado.
8. Consulte `/health` y confirme que PostgreSQL está saludable.

Documente el resultado, duración y cualquier discrepancia. Programe esta validación de restore periódicamente. Un restore que sólo recupera PostgreSQL pero no puede descargar adjuntos no es un restore completo.

## Seguridad de la operación

- Verifique que la cadena apunta a la base de restore antes de ejecutar `pg_restore --clean`.
- Nunca ejecute `DROP DATABASE`, `EnsureDeleted`, reset de tests ni comandos de cleanup sobre ambientes Development o Production.
- La suite de integración usa exclusivamente `ConnectionStrings:NakamaTestDatabase`, que debe ser una base desechable y separada.
