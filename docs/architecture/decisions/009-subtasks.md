# Subtasks

Subtasks are simple ordered checklist entries owned by a task. Task version is the concurrency boundary for their collection; completing all entries never changes task workflow. Progress is computed from subtasks at query time and is never persisted. Blocked tasks remain editable, while InReview, Completed and Cancelled are read-only. Deletion is physical until ActivityLog exists.
