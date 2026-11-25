# Nakama interface patterns

- Direction mirrors the Nakama attendance system. The current project or task is the main focus, with familiar navigation and plain Spanish controls.
- Tokens use navy for primary actions, yellow for orientation and selected navigation, and white surfaces over a cool canvas. Status colors convey state.
- Depth uses soft 1px borders and restrained elevation. The login uses the same navy two-panel composition as the attendance system.
- Spacing follows a 4px grid. Primary controls are at least 44px tall.
- Navigation uses a navy desktop sidebar, a white top bar, and a native dialog for the mobile menu. Project controls link to Tablero, Equipo, Configurar etapas and Historial.
- Motion uses short opacity and transform changes for page, dialog, and feedback entry. Respect reduced-motion settings.
- Components: native `dialog` BaseModal; compact form fields; member selectors always expose name and email rather than IDs; action buttons disable during requests.
- Concurrency: structural updates refresh from the backend. A 409 shows a human message and reloads rather than overwriting server state.
