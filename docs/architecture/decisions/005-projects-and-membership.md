# Projects and membership

## Context

Nakama needs projects and their participants before stages and tasks can be introduced.

## Decision

`Project` and `ProjectMember` belong to Projects; `User` remains in Identity. Global `UserRole` is distinct from `ProjectRole`. The project creator is stored temporarily in the request and becomes the initial Owner in the same persistence operation.

## Consequences

The Owner cannot be removed or transferred yet. Stages are the next planned iteration. Future authentication will replace `createdByUserId` in create-project requests with the authenticated user.
