# Cloudflare R2 para adjuntos privados

Nakama usa R2 únicamente a través de su API compatible con S3. Los bytes nunca se publican desde el bucket: el navegador carga y descarga siempre a través de la API autenticada de Nakama. La metadata sigue en PostgreSQL.

## Crear un bucket por instalación

1. Cree o abra la cuenta de Cloudflare y vaya a **R2 Object Storage**.
2. Cree un bucket privado para Empresa y otro para Universidad. `nakama-company-attachments` y `nakama-university-attachments` son sólo sugerencias; compruebe disponibilidad antes de elegirlos.
3. No active acceso público, `r2.dev`, dominio público ni URLs firmadas para esta iteración.
4. En R2, cree credenciales S3/API con el menor alcance posible para el bucket correspondiente: lectura, escritura y borrado de objetos. Guarde por separado el Access Key ID y Secret Access Key.
5. Copie el endpoint S3 del panel, el nombre del bucket y esas credenciales. No versionarlos ni colocarlos en variables `VITE_*`.

Configure sólo el servicio API de la instalación con:

```text
Attachments__Provider=S3
Attachments__S3__ServiceUrl=https://<account-id>.r2.cloudflarestorage.com
Attachments__S3__BucketName=<private-bucket-name>
Attachments__S3__AccessKeyId=<secret>
Attachments__S3__SecretAccessKey=<secret>
Attachments__S3__Region=auto
Attachments__MaxFileSizeBytes=10485760
```

`auto` es la región requerida por compatibilidad de R2, aunque R2 no la usa como una región AWS tradicional. La implementación configura el cliente AWS SDK con URL de servicio personalizada, path-style y los ajustes documentados por Cloudflare para cargas streaming. No hay Account ID, endpoint, bucket ni credenciales hardcodeados.

Valide desde Nakama, con un usuario autorizado, carga, descarga y eliminación. Revise que el objeto aparezca sólo en el bucket de esa instalación y que no exista ninguna URL pública. La key física es el nombre aleatorio ya generado por Nakama, nunca el nombre original del usuario.

La operación DB ↔ object storage no es atómica: al cargar, Nakama borra el objeto como compensación si falla el guardado de metadata; al borrar, primero elimina el objeto y después la metadata. Si el segundo paso falla, se requiere revisión operativa; no se usan transacciones distribuidas.

## Límites y coste del plan gratuito

Al momento de esta documentación, R2 incluye mensualmente uso Standard gratuito de 10 GB-mes, 1 millón de operaciones Class A y 10 millones de Class B; la salida directa de R2 no tiene coste de egress. Confirme los límites y cualquier cambio de facturación antes de operar: [precios oficiales de R2](https://developers.cloudflare.com/r2/pricing/). El límite de archivos de Nakama (por defecto 10 MiB) está por debajo de los límites de objeto de R2.

## Separación obligatoria

| Instalación | Bucket | Credenciales |
| --- | --- | --- |
| Company | bucket privado exclusivo | sólo company API y backup autorizado |
| University | bucket privado exclusivo | sólo university API y backup autorizado |

No reutilice un bucket ni credenciales entre las dos instalaciones.
