# Project stages

## Context

Projects need configurable ordered stages before tasks are introduced.

## Decision

Stage belongs to Projects and is not a task status. All stages share one contiguous project order. StageMember requires an existing ProjectMember; multiple Responsible members are allowed. Completed and Cancelled projects block structural changes, while Paused projects allow administration.

## Consequences

A ProjectMember cannot be removed while assigned to a stage. Tasks will be introduced in a later iteration.
