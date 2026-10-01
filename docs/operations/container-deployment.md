# Despliegue con contenedores

Nakama se despliega como tres servicios independientes: frontend Vue estático, API ASP.NET Core y PostgreSQL. Los adjuntos mantienen metadata en PostgreSQL y bytes en un volumen persistente externo; no se almacenan en la imagen ni en el filesystem efímero del contenedor.

## Dos instalaciones independientes

Este MVP se ejecuta como instalaciones aisladas, no como multi-tenancy dentro de una base:

| Instalación | Componentes aislados |
| --- | --- |
| Empresa | Nakama Web A, Nakama API A, PostgreSQL A, storage de attachments A y secretos A |
| Universidad | Nakama Web B, Nakama API B, PostgreSQL B, storage de attachments B y secretos B |

No mezcle Empresa y Universidad en una misma base de datos o volumen mientras no existan Workspaces. Este cambio no implementa Workspaces.

## Requisitos y configuración

Se necesita PostgreSQL, almacenamiento persistente para `/data/attachments`, terminación HTTPS/reverse proxy externo y un almacén de secretos. No se requiere ni se configura un proveedor cloud específico.

Configure en el host de la API:

| Variable | Ejemplo seguro de forma |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ConnectionStrings__NakamaDatabase` | `Host=...;Port=5432;Database=...;Username=...;Password=...;SSL Mode=Require` |
| `Authentication__Jwt__SigningKey` | valor aleatorio de al menos 32 bytes |
| `Authentication__Jwt__Issuer` | `https://api.nakama.example.com` |
| `Authentication__Jwt__Audience` | `https://api.nakama.example.com` |
| `Authentication__Jwt__AccessTokenMinutes` | `60` |
| `Cors__AllowedOrigins__0` | `https://nakama.example.com` |
| `AllowedHosts` | `api.nakama.example.com` |
| `Attachments__Provider` | `Local` o `S3` |
| `Attachments__StoragePath` | `/data/attachments` (sólo `Local`) |
| `Attachments__S3__ServiceUrl` | URL HTTPS del endpoint S3-compatible (sólo `S3`) |
| `Attachments__S3__BucketName` | bucket privado (sólo `S3`) |
| `Attachments__S3__AccessKeyId` / `Attachments__S3__SecretAccessKey` | credenciales server-side (sólo `S3`) |
| `Attachments__S3__Region` | región del proveedor; para R2, `auto` |

Genere la clave JWT localmente sin guardarla en el repositorio:

```powershell
./scripts/Generate-NakamaSecrets.ps1
```

`VITE_API_BASE_URL` es configuración de build del frontend, por ejemplo `https://api.nakama.example.com`. No es el origen CORS: `Cors__AllowedOrigins__0` debe ser el origen del frontend, `https://nakama.example.com`. Mantenga HTTPS, cookies `HttpOnly`/`Secure`/`SameSite=None`, `credentials: include` y `X-Nakama-Csrf`; la containerización no modifica esas protecciones.

## Construcción y despliegue

Construya la API desde la raíz del repositorio:

```powershell
docker build -f src/backend/Nakama.Api/Dockerfile -t nakama-api:latest .
```

La API escucha en `0.0.0.0:$PORT` cuando el host inyecta `PORT`; si no existe, la imagen usa el puerto 8080. Para `Attachments__Provider=Local`, monte un volumen persistente en `/data/attachments` y configure `Attachments__StoragePath=/data/attachments`. Para `S3`, no dependa del filesystem del contenedor: consulte [Cloudflare R2](cloudflare-r2.md).

Construya el frontend de forma independiente:

```powershell
docker build --build-arg VITE_API_BASE_URL=https://api.nakama.example.com -t nakama-web:latest ./src/frontend
```

El nginx incluido sirve assets y devuelve `index.html` para rutas Vue como `/projects/123`. No proxyfica la API: publique ambos servicios por separado detrás de dominios HTTPS.

El archivo `compose.yaml` permite verificar una arquitectura local similar a Production. Copie `.env.example` a un `.env` no versionado, sustituya todos los placeholders, y ejecute `docker compose config` antes de levantarlo. Los volúmenes nombrados `nakama-postgres` y `nakama-attachments` representan los dos datos persistentes requeridos. Compose no aplica migraciones ni provisiona usuarios.

## Migraciones y primer administrador

Antes de desplegar una versión: haga backup, configure la cadena de la DB objetivo, aplique migraciones, despliegue la nueva API y valide readiness. La API nunca ejecuta migraciones automáticamente al iniciar.

```powershell
dotnet ef database update --project src/backend/Nakama.Api --startup-project src/backend/Nakama.Api
```

Después de una migración exitosa y antes de abrir el acceso de una instalación nueva, ejecute explícitamente el provisionador desde un entorno controlado con `ConnectionStrings__NakamaDatabase` apuntando a esa instalación:

```powershell
dotnet run --project src/backend/Nakama.Provisioning -- create-first-admin --name "Administrador" --email "admin@nakama.local"
```

El comando valida y normaliza los datos como Identity, crea un Admin activo y genera una contraseña temporal criptográficamente segura. La imprime una sola vez tras el commit; guárdela inmediatamente en un gestor de contraseñas. Sólo se persiste `PasswordHash`.

Es conservador: falla sin modificar datos si el email existe o si ya hay un Admin activo. No se ejecuta al iniciar la API, Docker o Compose. `DevelopmentBootstrap` continúa limitado a Development.

Si el operador debe imponer una contraseña inicial en vez de recibir una temporal, configure de forma sellada y **sólo para el provisionamiento** `Provisioning__FirstAdmin__Password`. El provisionador la valida (mínimo ocho caracteres), la convierte directamente en `PasswordHash` y nunca la imprime. El valor debe eliminarse inmediatamente después de que el comando termine; no lo deje disponible para la API en ejecución. Dentro de la imagen de API, ejecute el provisionador así:

```sh
dotnet /app/provisioning/Nakama.Provisioning.dll create-first-admin --name "<nombre>" --email "<correo>"
```

### Railway: migración antes de activar la API

La imagen de `Nakama.Api` incluye un bundle EF (`/app/efbundle`) exclusivamente para la fase pre-deploy. Configure en Railway el siguiente **Pre-deploy command** en el servicio de API:

```sh
./efbundle --connection "$ConnectionStrings__NakamaDatabase"
```

El comando se ejecuta dentro de la red privada y debe recibir la cadena mediante una variable de referencia al servicio PostgreSQL; no habilite acceso TCP público a PostgreSQL. Railway cancela el deployment si el bundle falla, de modo que una migración no aplicada nunca deja la nueva API activa. Configure un timeout explícito razonable para el tamaño de la base y consulte el estado de `__EFMigrationsHistory` después del deployment.

Para el health check de Railway configure `/health/ready`. Como Railway usa el host `healthcheck.railway.app`, incluya ese host además del hostname público de la API en `AllowedHosts`; de lo contrario ASP.NET Core puede responder `400` y Railway marcará el deployment como fallido. El volumen de adjuntos debe montarse en `/data/attachments`; no use el filesystem efímero del contenedor.

## Validación operativa

Configure los dominios, TLS y proxy externo. El proxy debe conservar cookies, cuerpo multipart y `X-Nakama-Csrf`. Configure liveness en `/health/live` y readiness en `/health/ready`. Como smoke test, compruebe ambos endpoints, inicie sesión, cree una tarea, cargue y descargue un adjunto, y confirme que la SPA resuelve una ruta profunda.

## Backups

Un backup completo siempre incluye PostgreSQL **y** los objetos de attachments. Consulte [backup y restore](backup-restore.md): incluye `pg_dump`, restore aislado, copia de volumen o bucket y validación periódica de una restauración. Un dump de DB sin adjuntos no es un backup completo de Nakama.
