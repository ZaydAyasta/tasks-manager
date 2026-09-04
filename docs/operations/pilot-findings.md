# Registro de hallazgos del piloto UAT

Los hallazgos se registran con pasos reproducibles. No se anotan passwords, tokens, connection strings ni datos personales fuera de las identidades sintéticas documentadas.

## UAT-001

- **Severity:** P1
- **Area:** Helper de datos de piloto Development
- **Steps:** Habilitar `DevelopmentBootstrap:PilotData` con proyecto existente y ejecutar `dotnet run --project src/backend/Nakama.Api --no-launch-profile`.
- **Expected:** La API compila y el helper puede preparar el dataset con el creador de tarea correcto.
- **Actual:** La primera implementación de la ruta de adopción no compilaba por una referencia de creador fuera de ámbito.
- **Status:** Fixed. El creador se pasa explícitamente al helper de tarea; build, tres pruebas focalizadas y ejecución Development posterior fueron exitosos.

## UAT-002

- **Severity:** P2
- **Area:** Preparación de datos con proyecto local existente
- **Steps:** Ejecutar el seed habilitado cuando ya existe un proyecto activo llamado `Implementación Portal de Clientes` que no tiene el marcador del helper.
- **Expected:** No adoptar ni modificar silenciosamente un proyecto Development no confirmado.
- **Actual:** El helper se detuvo antes de escribir datos y reportó el conflicto de proyecto.
- **Status:** Closed as designed. Se añadió `DevelopmentBootstrap:PilotData:AdoptExistingProject=true` como aprobación explícita, documentada y limitada a Development.

## Resumen actual

No hay hallazgos P0/P1 abiertos tras la validación técnica del 2026-09-03. La checklist manual continúa siendo la evidencia de aceptación por usuarios del piloto.
