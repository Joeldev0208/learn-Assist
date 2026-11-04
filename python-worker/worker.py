#!/usr/bin/env python3
"""Minimal JSON Lines worker for Learn-Assist.

Stdout is reserved for protocol responses. Diagnostics belong on stderr.
"""

import json
import sys


def respond(request):
    message = str(request.get("message", "")).strip()
    if not message:
        return {"error": "La solicitud está vacía."}

    history = request.get("history", [])
    user_turns = sum(1 for item in history if item.get("role") == 1)
    return {
        "id": request.get("id"),
        "text": (
            "Plan local de Python: analizaré la pregunta y usaré el contexto "
            f"reciente disponible ({user_turns} turnos). Pregunta recibida: {message}"
        ),
    }


for line in sys.stdin:
    try:
        request = json.loads(line)
        print(json.dumps(respond(request), ensure_ascii=False), flush=True)
    except Exception as error:
        print(json.dumps({"error": "Solicitud Python inválida."}), flush=True)
        print(f"worker error: {error}", file=sys.stderr, flush=True)
