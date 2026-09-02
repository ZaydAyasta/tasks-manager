# 012 - Authentication and authorization baseline

Nakama keeps its existing domain `User` instead of adopting the full ASP.NET Core Identity schema. The current model has domain-specific lifecycle and roles; adding Identity tables would duplicate it without a current requirement for external providers or account workflows.

Passwords are persisted only as `PasswordHash`, generated and verified by ASP.NET Core `IPasswordHasher<User>`. The API uses short-lived JWT bearer access tokens (default 60 minutes) with only `sub`, email and role claims. The signing key is supplied by user-secrets or environment variables and is never committed.

`ICurrentUser` reads claims once from `HttpContext`. `AuthenticatedUser` and `Admin` policies centrally verify that the database user still exists and is active; the Admin policy also reads the current database role. This prevents a deactivated user or a role change from retaining privileges from an already-issued token.

Human actor identifiers no longer come from requests. Activity recording derives the actor from `ICurrentUser` and uses the same EF DbContext as the domain write, preserving atomicity. Target identifiers such as assignee and member remain request data.

Admin owns structural administration. Collaborators use protected work endpoints, where existing project membership checks remain domain rules; global roles do not replace membership.

No refresh tokens are implemented. A future SPA needs a session/refresh-token strategy; SSO, Entra ID and other external identity providers remain future business decisions.
