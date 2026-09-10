# Despliegue para piloto interno

## Alcance y topología

El piloto usa tres piezas independientes: archivos estáticos del frontend Vue, API ASP.NET Core .NET 10 y PostgreSQL. Los adjuntos se almacenan en un directorio persistente del servidor; sus bytes no se guardan en PostgreSQL.

En Windows, una opción simple es servir el frontend estático desde el servidor web corporativo existente y ejecutar la API como Windows Service detrás de un reverse proxy HTTPS. IIS con ARR es una alternativa válida si ya está aprobado por infraestructura. La terminación TLS, DNS, certificados, firewall y cuentas de servicio son decisiones del equipo de infraestructura; Nakama no las configura automáticamente.

## Prerrequisitos

- .NET 10 Runtime para la API.
- PostgreSQL accesible desde la cuenta que ejecuta la API.
- Node.js 24.15.0 o compatible (`^22.22.2` o `>=24.15.0`) sólo para generar el artefacto del frontend; `src/frontend/.nvmrc` fija la versión recomendada.
- Un directorio persistente de adjuntos con permisos de lectura, escritura y borrado para la cuenta de la API.
- Un proxy o servidor web que entregue HTTPS al navegador y reenvíe la API sin eliminar cookies, `X-Nakama-Csrf` ni el cuerpo multipart.

## Configuración de la API

Production no toma secretos desde el repositorio ni usa defaults de localhost. Configure estas variables mediante el mecanismo seguro del host:

| Variable | Requerida | Uso |
| --- | --- | --- |
| `ASPNETCORE_ENVIRONMENT=Production` | Sí | Activa las validaciones de Production y deshabilita el bootstrap de Admin. |
| `ConnectionStrings__NakamaDatabase` | Sí | Cadena de conexión PostgreSQL de la base del piloto. |
| `Authentication__Jwt__SigningKey` | Sí | Clave secreta aleatoria de al menos 32 bytes para firmar JWT. No se registra ni se guarda en el repositorio. |
| `Authentication__Jwt__Issuer` | Sí | Emisor esperado de los JWT. |
| `Authentication__Jwt__Audience` | Sí | Audiencia esperada de los JWT. |
| `Authentication__Jwt__AccessTokenMinutes` | Sí | Duración positiva del access token. |
| `Authentication__LoginRateLimit__PermitLimit` | No | Intentos de login por cuenta/origen dentro de la ventana; default 10. |
| `Authentication__LoginRateLimit__WindowSeconds` | No | Duración de la ventana de login; default 60 segundos. |
| `Cors__AllowedOrigins__0` | Sí | Origen HTTPS exacto del frontend; agregue índices adicionales para más orígenes. |
| `AllowedHosts` | Sí | Lista separada por `;` de los hostnames públicos de la API; no use `*`. |
| `Attachments__StoragePath` | Sí | Ruta absoluta y persistente de adjuntos, por ejemplo una unidad administrada fuera del directorio de despliegue. |
| `Attachments__MaxFileSizeBytes` | No | Límite por archivo; el default es 10 MB. |

La API falla durante el arranque si faltan DB, JWT, CORS, `AllowedHosts` o ruta de adjuntos; también falla si la clave JWT tiene menos de 32 bytes, si CORS no usa HTTPS fuera de Development, si `AllowedHosts` permite cualquier host, o si los límites de login no son válidos. En Production, `Attachments__StoragePath` debe ser absoluta. `DevelopmentBootstrap__Admin__*` se ignora fuera de Development y no debe configurarse como un mecanismo de Production.

`appsettings.Development.json` contiene únicamente conveniencias locales: metadatos JWT de desarrollo, `http://localhost:5173` y `App_Data/attachments`. No copie esos valores a Production.

## Migraciones

La API no ejecuta `database update` al arrancar. Antes de publicar una versión, el operador autorizado debe aplicar las migraciones contra la base objetivo y confirmar backup disponible:

```powershell
dotnet ef database update --project src/backend/Nakama.Api --startup-project src/backend/Nakama.Api
```

Ejecute este comando desde un entorno controlado con la configuración Production anterior. Nunca use la infraestructura de pruebas ni `EnsureDeleted` contra una base Development o Production.

## Publicación de backend

```powershell
dotnet publish src/backend/Nakama.Api -c Release -o .\artifacts\nakama-api
```

Instale o actualice el servicio con las variables de entorno seguras ya definidas. El reverse proxy debe usar HTTPS hacia el navegador y la API no debe quedar publicada directamente en HTTP. Si el proxy termina TLS antes de la API, configure el host/proxy conforme a la política corporativa y valide las cabeceras reenviadas; no confíe en cabeceras públicas sin esa configuración de infraestructura.

Use `GET /health/live` para liveness del proceso: no depende de PostgreSQL. Use `GET /health/ready` para readiness: comprueba PostgreSQL cuando la cadena está configurada. `GET /health` conserva el comportamiento de readiness por compatibilidad. Ninguno publica connection strings, rutas ni secretos.

Serilog escribe eventos de inicio, solicitudes y excepciones no controladas. Los `ProblemDetails` de cliente son genéricos y sólo incluyen un `traceId`; use ese identificador para correlacionar con logs. No registre passwords, JWT completos, bytes de adjuntos, contenido de comentarios o secretos.

## Autenticación de navegador y protección CSRF

En Production, el login entrega el JWT como cookie `HttpOnly`, `Secure` y con `SameSite=None`; el cuerpo de la respuesta no incluye el access token. La SPA usa `credentials: include`, obtiene un token CSRF ligado a la sesión desde login o `GET /api/auth/me`, y lo envía como `X-Nakama-Csrf` en toda operación autenticada no segura. La API rechaza con `403` una mutación autenticada que no presente ese valor.

El frontend y la API deben estar ambos en HTTPS. La política CORS permite credenciales sólo para los orígenes explícitos configurados; nunca configure `*`. La API aplica `Cache-Control: no-store`, `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, CSP restrictiva y HSTS a sus respuestas `/api` en Production. El hosting del frontend debe servir por separado una Content Security Policy adecuada a su dominio y a `VITE_API_BASE_URL`, además de `X-Content-Type-Options: nosniff`, `frame-ancestors 'none'` y una política de referrer restrictiva.

El límite de login de la API se particiona por origen y cuenta, y es seguro ante la creación concurrente de una misma partición, pero protege una sola instancia. Si se despliegan varias instancias, el proxy/WAF debe aplicar además una cuota coherente en `/api/auth/login`; no dependa del límite en memoria como única barrera perimetral.

## Publicación de frontend

```powershell
Set-Location src/frontend
npm ci
$env:VITE_API_BASE_URL = "https://api.nakama.internal"
npm run build
```

Publique `src/frontend/dist/` en el hosting estático elegido. `VITE_API_BASE_URL` es obligatorio y debe ser una URL HTTPS absoluta sin credenciales, query ni fragment en el build de Production; el build falla si apunta a HTTP o localhost. El valor debe coincidir con una entrada de `Cors__AllowedOrigins__*` en la API por su origen del navegador.

## Adjuntos y seguridad de archivos

La carpeta de adjuntos debe quedar fuera de `bin`, `temp` y cualquier filesystem efímero del deployment. No se sirve como contenido estático: las descargas pasan por autenticación y membresía del proyecto. La implementación usa nombres físicos GUID, rechaza path traversal, aplica allowlist de tipo/extensión y limita tamaño. El análisis antimalware externo no existe aún; es hardening futuro que debe decidirse antes de aceptar archivos de fuentes no confiables.
