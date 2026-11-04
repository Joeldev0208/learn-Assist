# Learn-Assist Python Worker

Worker local mínimo para Learn-Assist. Usa JSON Lines por `stdin/stdout`:

```json
{"id":"1","message":"¿Qué es Python?","history":[],"model":"local-planner"}
```

La respuesta mantiene la misma línea:

```json
{"id":"1","text":"..."}
```

No requiere paquetes externos. `stdout` está reservado para el protocolo; los diagnósticos se escriben en `stderr`.
