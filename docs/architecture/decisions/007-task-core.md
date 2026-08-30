# Task core

## Context

Tasks need their own lifecycle and assignees while consuming Project and Stage references.

## Decision

Tasks are implemented in the independent Tasks module as `WorkTask` to avoid the .NET Task name collision. A Task may have zero or more assignees, each an existing ProjectMember; StageMember is not required. Workflow is Pending, InProgress, InReview and Completed.

## Consequences

Complete represents future authorized approval and is not protected yet. Blocked remains reserved for TaskBlocker. Subtasks and dependencies are deferred; createdByUserId is temporary until authentication.
