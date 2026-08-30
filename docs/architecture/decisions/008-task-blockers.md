# Task blockers

## Context

Tasks need to retain operational blockers and resume their previous workflow state only when every active blocker is resolved.

## Decision

`TaskBlocker` is an append-only historical record until resolution, with a fixed enum category. Multiple active blockers are allowed. A task enters `Blocked` when the first blocker is reported and stores its prior state. Resolving the final active blocker restores that state. Reporters and resolvers must be active ProjectMembers; authorization policies will be added with authentication.

## Consequences

Cancelling a blocked task is terminal: blockers remain as history and later resolution never reactivates it. Blocker operations use the task version as the aggregate concurrency boundary. Subtasks, dependencies, comments, notifications and events remain deferred.
