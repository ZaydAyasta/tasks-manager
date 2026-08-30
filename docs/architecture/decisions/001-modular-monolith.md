# Modular monolith

## Context

Nakama comienza como una aplicación interna con dominios relacionados y sin necesidades operativas de microservicios.

## Decision

Se usa una única API ASP.NET Core, separada por módulos y namespaces. Los módulos se componen mediante extensiones de servicios y endpoints.

## Consequences

Se conservan límites de dominio sin la complejidad distribuida. Identity queda preparado; se evaluarán ASP.NET Core Identity y Microsoft Entra ID si la empresa usa Microsoft 365.
