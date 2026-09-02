# Nakama interface patterns

- Direction: calm, dense B2B project control. The focal point is the current project or task, while management actions stay grouped at the edge.
- Tokens: `--blue` is the primary action, `--yellow` remains the identity accent, status colours convey state only, and white paper surfaces sit over `--canvas`.
- Depth: soft 1px borders and restrained elevation on hover; no heavy panels or gradients for management controls.
- Spacing: 4px grid; 14px task-card padding, 16–22px configuration panels, 48px desktop page padding.
- Navigation: left sidebar for product context; local underlined tabs for Board, Members and Configuration.
- Components: native `dialog` BaseModal; compact form fields; member selectors always expose name and email rather than IDs; action buttons disable during requests.
- Concurrency: structural updates refresh from the backend. A 409 shows a human message and reloads rather than overwriting server state.
