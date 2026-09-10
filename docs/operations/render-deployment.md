# Render: dos instalaciones independientes

No se incluye `render.yaml`: un template que cree Company y University a la vez haría más fácil compartir por error secretos o recursos. Repita la configuración manual siguiente por instalación y mantenga recursos separados.

| Instalación | Static Site | Web Service | Neon | R2 | secretos |
| --- | --- | --- | --- | --- | --- |
| Company | `company-web` | `company-api` | project/database company | bucket company | sólo company |
| University | `university-web` | `university-api` | project/database university | bucket university | sólo university |

## Frontend: Static Site

En Render, conecte el repositorio GitHub y cree un **Static Site** por instalación. Seleccione la rama deseada y configure:

| Campo | Valor |
| --- | --- |
| Root Directory | `src/frontend` |
| Build Command | `npm ci && npm run build` |
| Publish Directory | `dist` |
| Build environment variable | `VITE_API_BASE_URL=https://<api>.onrender.com` |

Configure una rewrite SPA `/*` → `/index.html` si Render la solicita para la ruta profunda de Vue. Render entrega HTTPS y un dominio `onrender.com` inicial; ese dominio es válido para esta etapa. `VITE_API_BASE_URL` sólo puede contener la URL pública de la API, nunca secretos ni claves R2.

El Dockerfile del frontend se mantiene para portabilidad, pero Render Static Site no necesita nginx ni un Web Service para servir Vue.

## API: Web Service Free

Cree un **Web Service** por instalación, conecte el mismo repositorio y rama, seleccione Docker, con:

| Campo | Valor |
| --- | --- |
| Dockerfile path | `src/backend/Nakama.Api/Dockerfile` |
| Docker build context | raíz del repositorio |
| Environment | `Production` |
| Health check path | `/health/ready` |

Render proporciona `PORT`; la API ya escucha en `0.0.0.0:$PORT` y mantiene 8080 para ejecución local/contenedor. No añada lógica de Render al dominio.

Defina secretos/variables exclusivamente en cada API:

```text
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__NakamaDatabase=<NEON CONNECTION STRING>
Authentication__Jwt__SigningKey=<at least 32 random bytes>
Authentication__Jwt__Issuer=Nakama
Authentication__Jwt__Audience=NakamaWeb
Authentication__Jwt__AccessTokenMinutes=<value>
Cors__AllowedOrigins__0=<RENDER FRONTEND URL>
AllowedHosts=<RENDER API HOSTNAME>
Attachments__Provider=S3
Attachments__S3__ServiceUrl=<R2 ENDPOINT>
Attachments__S3__BucketName=<BUCKET>
Attachments__S3__AccessKeyId=<SECRET>
Attachments__S3__SecretAccessKey=<SECRET>
Attachments__S3__Region=auto
Attachments__MaxFileSizeBytes=<VALUE>
```

Despliegue sólo después de aplicar migraciones y provisionar el primer administrador según [Neon](neon.md). Valide `/health/ready`, login, CSRF, carga/descarga/eliminación de adjunto y una ruta SPA profunda. No almacene archivos locales: el filesystem de Free es efímero.

## Limitaciones Free

Un Web Service Free puede dormirse tras inactividad y el primer request puede tardar aproximadamente un minuto en reactivarlo. CPU/RAM y horas mensuales son limitados; Render puede reiniciar el servicio, y no hay disco persistente en Web Service Free. Static Site no usa runtime de Web Service. No se implementa keep-alive artificial. Verifique condiciones actuales en la [documentación Free de Render](https://render.com/docs/free).

Seguridad: use HTTPS, CORS con el origen exacto de cada frontend, `AllowedHosts` con el hostname de su API y no use wildcard CORS. Cookies HttpOnly/Secure y CSRF se conservan. El frontend no recibe connection strings ni credenciales R2.
