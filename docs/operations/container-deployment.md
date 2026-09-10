# Despliegue con contenedores

Nakama se despliega como tres servicios independientes: frontend Vue estático, API ASP.NET Core y PostgreSQL. Los adjuntos mantienen metadata en PostgreSQL y bytes en un volumen persistente externo; no se almacenan en la imagen ni en el filesystem efímero del contenedor.

## Dos instalaciones independientes

Este MVP se ejecuta como instalaciones aisladas, no como multi-tenancy dentro de una base:

| Instalación | Componentes aislados |
| --- | --- |
| Empresa | Nakama Web A, Nakama API A, PostgreSQL A, volumen Attachments A y secretos A |
| Universidad | Nakama Web B, Nakama API B, PostgreSQL B, volumen Attachments B y secretos B |

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
| `Attachments__StoragePath` | `/data/attachments` |

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

La API escucha en `0.0.0.0:$PORT` cuando el host inyecta `PORT`; si no existe, la imagen usa el puerto 8080. Monte un volumen persistente en `/data/attachments` y configure `Attachments__StoragePath=/data/attachments`.

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

## Validación operativa

Configure los dominios, TLS y proxy externo. El proxy debe conservar cookies, cuerpo multipart y `X-Nakama-Csrf`. Configure liveness en `/health/live` y readiness en `/health/ready`. Como smoke test, compruebe ambos endpoints, inicie sesión, cree una tarea, cargue y descargue un adjunto, y confirme que la SPA resuelve una ruta profunda.

## Backups

Un backup completo siempre incluye PostgreSQL **y** el volumen de attachments. Consulte [backup y restore](backup-restore.md): incluye `pg_dump`, restore aislado, snapshot/copia del volumen y validación periódica de una restauración. Un dump de DB sin adjuntos no es un backup completo de Nakama.
