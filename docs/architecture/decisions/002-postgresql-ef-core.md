# PostgreSQL and EF Core

## Context

Nakama necesita persistencia relacional y timestamps consistentes.

## Decision

Se usa EF Core con Npgsql y un único `NakamaDbContext`. Los instantes se modelarán con `DateTimeOffset` y PostgreSQL `timestamptz`, en UTC.

## Consequences

No se añade un repositorio genérico ni Unit of Work adicional. La cadena se suministra mediante configuración, variables de entorno o user-secrets, nunca en el código.
