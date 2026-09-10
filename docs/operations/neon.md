# Neon PostgreSQL

Nakama ya usa PostgreSQL mediante Npgsql y EF Core; Neon no requiere cambios en el dominio ni en la persistencia.

1. Cree una cuenta Neon.
2. Cree un proyecto/database independiente para Company (sugerencia: `nakama-company`).
3. Cree otro proyecto/database independiente para University (sugerencia: `nakama-university`).
4. Copie la cadena de conexión que entrega Neon y configúrela sólo en la API, CLI de provisionamiento o sesión temporal de migración correspondiente:

   ```text
   ConnectionStrings__NakamaDatabase=<Neon connection string>
   ```

Npgsql acepta los connection strings PostgreSQL que entrega Neon; no se añadió un parser propio. Conserve TLS/SSL tal como venga en la cadena de Neon. Nunca comparta una cadena entre Company y University.

## Migraciones seguras

Antes del primer deploy y para cada cambio de esquema, apunte temporalmente el entorno local a la instancia Neon correcta y ejecute:

```powershell
dotnet ef database update --project src/backend/Nakama.Api --startup-project src/backend/Nakama.Api
dotnet ef migrations has-pending-model-changes --project src/backend/Nakama.Api --startup-project src/backend/Nakama.Api
```

No ejecute `EnsureDeleted`, no reutilice la DB de tests y no aplique migraciones durante el arranque de Render.

## Primer administrador

Tras las migraciones, ejecute el provisionador con `ConnectionStrings__NakamaDatabase` apuntando explícitamente a la base objetivo:

```powershell
dotnet run --project src/backend/Nakama.Provisioning -- create-first-admin --name "Administrador" --email "admin@nakama.local"
```

El ejemplo es para Company. Para University, el operador debe definir primero el nombre y email definitivos; no se presupone ninguno. La contraseña temporal se muestra una sola vez por el CLI y debe guardarse en un gestor de contraseñas.

## Plan Free

Neon Free está sujeto a límites de storage y compute; el compute puede escalar a cero cuando está inactivo, por lo que puede haber latencia de reactivación. Revise antes de operar los límites vigentes de [Neon Free](https://neon.com/pricing). Mantenga dos proyectos independientes, incluso en plan gratuito.
