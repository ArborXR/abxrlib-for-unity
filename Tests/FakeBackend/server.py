"""
AbxrLib fake backend for integration tests.

A small HTTP server (stdlib only — no pip dependencies) that mimics the AbxrLib
REST endpoints so PlayMode integration tests can drive authentication, config, data, and
storage flows end-to-end without hitting a real server.

The wire format mirrors the real FastAPI backend:
  - camelCase field names (via pyhumps in the real backend; emitted directly here)
  - 201 status for POST creates, 200 for GET / DELETE
  - error bodies as {"detail": "..."} (matches FastAPI's default HTTPException shape)

Two flavors of endpoints:

  /v1/...              -- the endpoints AbxrLib actually calls (auth, config, data, storage)
  /__control/...       -- test harness endpoints to script behavior and inspect what arrived

A test typically:
  1. POST /__control/reset                 -- clear scenario + recorded requests
  2. POST /__control/scenario              -- set the response for /v1/auth/token (or any other endpoint)
  3. Triggers the Unity code under test
  4. GET  /__control/requests              -- assert which requests were made

Run standalone with `python server.py --port 8765`. The C# harness boots this
process automatically; you only run it by hand when iterating on the server itself.
"""

import argparse
import base64
import json
import sys
import threading
import time
from collections import defaultdict
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from typing import Any, Optional, Tuple
from urllib.parse import parse_qs, urlsplit


# ── State ────────────────────────────────────────────────────────────────────

class State:
    """All mutable test state. Lives in-process; reset between tests via /__control/reset."""

    def __init__(self) -> None:
        self._lock = threading.Lock()
        # Configured responses keyed by (method, path). Each entry is a list of responses
        # consumed in order; the last entry sticks once the list is exhausted (so a single
        # configured response applies to every subsequent call).
        self._scenarios: dict[Tuple[str, str], list[dict]] = defaultdict(list)
        # Every request that arrived at a /v1/... endpoint, in order.
        self._requests: list[dict] = []

    def reset(self) -> None:
        with self._lock:
            self._scenarios.clear()
            self._requests.clear()

    def queue_response(self, method: str, path: str, response: dict) -> None:
        with self._lock:
            self._scenarios[(method.upper(), path)].append(response)

    def take_response(self, method: str, path: str) -> Optional[dict]:
        with self._lock:
            queue = self._scenarios.get((method.upper(), path))
            if not queue:
                return None
            # Pop unless this is the last one, in which case keep it as a sticky default.
            return queue.pop(0) if len(queue) > 1 else queue[0]

    def record(self, req: dict) -> None:
        with self._lock:
            self._requests.append(req)

    def snapshot_requests(self, path_filter: Optional[str] = None) -> list[dict]:
        with self._lock:
            if path_filter is None:
                return list(self._requests)
            return [r for r in self._requests if r["path"] == path_filter]


STATE = State()


# ── Helpers ──────────────────────────────────────────────────────────────────

def _b64url(data: bytes) -> str:
    return base64.urlsafe_b64encode(data).rstrip(b"=").decode("ascii")


def make_jwt(claims: Optional[dict] = None, exp_seconds_from_now: int = 3600) -> str:
    """Build a structurally-valid JWT. Signature is fake; AbxrLib only decodes the payload."""
    header = {"typ": "JWT", "alg": "HS256"}
    payload = dict(claims or {})
    payload.setdefault("exp", int(time.time()) + exp_seconds_from_now)
    h = _b64url(json.dumps(header, separators=(",", ":")).encode("utf-8"))
    p = _b64url(json.dumps(payload, separators=(",", ":")).encode("utf-8"))
    s = _b64url(b"fake-signature-for-testing")
    return f"{h}.{p}.{s}"


def default_auth_response() -> dict:
    """Default AuthenticationResponseSchema. camelCase fields match the real FastAPI backend
    (see lib-backend/code/app/auth/schemas.py::AuthenticationResponseSchema)."""
    return {
        "token": make_jwt(),
        "secret": "test-secret",
        "userId": "test-user-id",
        "userData": {"email": "test@example.com"},
        "appId": "00000000-0000-0000-0000-000000000001",
        "packageName": "com.example.testapp",
        "modules": [],
    }


def default_config_response() -> dict:
    """A minimal /v1/storage/config payload. Empty is valid; ApplyConfigPayload only merges set fields."""
    return {}


def default_storage_response() -> dict:
    """Default storage GET response shape (see lib-backend/code/app/storage/router.py::get_storage)."""
    return {"data": []}


# ── Request handler ──────────────────────────────────────────────────────────

class Handler(BaseHTTPRequestHandler):
    """Single handler that dispatches by (method, path). Stdlib http.server doesn't have
    a routing decorator, so the dispatch table is explicit. Each AbxrLib endpoint calls
    record_request() then respond(); each control endpoint manipulates STATE directly."""

    # Silence the default access-log noise on stderr; the C# harness captures stderr
    # and we'd rather only see real errors there.
    def log_message(self, fmt: str, *args: Any) -> None:
        return

    # ── Plumbing ─────────────────────────────────────────────────────

    def _read_body(self) -> str:
        length = int(self.headers.get("Content-Length") or 0)
        if length <= 0:
            return ""
        return self.rfile.read(length).decode("utf-8", errors="replace")

    def _read_json_body(self) -> Any:
        body = self._read_body()
        if not body:
            return None
        try:
            return json.loads(body)
        except json.JSONDecodeError:
            return None

    def _write_json(self, status: int, payload: Any) -> None:
        body = json.dumps(payload).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def _write_raw(self, status: int, raw: str, content_type: str) -> None:
        body = raw.encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", content_type)
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def _write_empty(self, status: int) -> None:
        self.send_response(status)
        self.send_header("Content-Length", "0")
        self.end_headers()

    def _record_request(self, parsed_path: str, query: dict) -> None:
        body_text = self._read_body()
        try:
            body_json = json.loads(body_text) if body_text else None
        except json.JSONDecodeError:
            body_json = None
        STATE.record({
            "method": self.command,
            "path": parsed_path,
            "headers": {k: v for k, v in self.headers.items()},
            "query": query,
            "body_text": body_text,
            "body_json": body_json,
            "received_at": time.time(),
        })

    def _respond_from_scenario(self, method: str, path: str, default_body: Any, default_status: int = 200) -> None:
        """Look up a scripted response for (method, path), or fall back to a default."""
        scenario = STATE.take_response(method, path)
        if scenario is None:
            self._write_json(default_status, default_body)
            return

        delay_ms = scenario.get("delay_ms", 0)
        if delay_ms:
            time.sleep(delay_ms / 1000.0)

        # If a scenario specifies status, use it; otherwise inherit the endpoint's default.
        status = int(scenario.get("status", default_status))

        # Allow raw string responses (e.g. malformed JSON, empty body) via "raw" field.
        if "raw" in scenario:
            self._write_raw(status, scenario["raw"], scenario.get("content_type", "application/json"))
            return

        # "body" present in scenario takes precedence; absent means "use default body".
        # body=None explicitly in the scenario means "return an empty body" (status only).
        if "body" in scenario:
            body = scenario["body"]
            if body is None:
                self._write_empty(status)
            else:
                self._write_json(status, body)
            return

        self._write_json(status, default_body)

    # ── Dispatch ─────────────────────────────────────────────────────

    def do_GET(self) -> None:  # noqa: N802 (BaseHTTPRequestHandler API)
        self._dispatch("GET")

    def do_POST(self) -> None:  # noqa: N802
        self._dispatch("POST")

    def do_PUT(self) -> None:  # noqa: N802
        self._dispatch("PUT")

    def do_DELETE(self) -> None:  # noqa: N802
        self._dispatch("DELETE")

    def do_PATCH(self) -> None:  # noqa: N802
        self._dispatch("PATCH")

    def _dispatch(self, method: str) -> None:
        url = urlsplit(self.path)
        path = url.path
        query = {k: v[0] if len(v) == 1 else v for k, v in parse_qs(url.query).items()}

        # ── Control plane (never recorded) ────────────────────────────

        if path == "/__control/health" and method == "GET":
            self._write_json(200, {"ok": True})
            return
        if path == "/__control/reset" and method == "POST":
            self._read_body()  # drain
            STATE.reset()
            self._write_json(200, {"ok": True})
            return
        if path == "/__control/scenario" and method == "POST":
            payload = self._read_json_body() or {}
            scenario_path = payload.get("path")
            if not scenario_path:
                self._write_json(400, {"error": "path is required"})
                return
            scenario_method = payload.get("method", "POST")
            scenario = {k: payload[k] for k in ("status", "body", "raw", "content_type", "delay_ms") if k in payload}
            STATE.queue_response(scenario_method, scenario_path, scenario)
            self._write_json(200, {"ok": True})
            return
        if path == "/__control/requests" and method == "GET":
            path_filter = query.get("path")
            self._write_json(200, {"requests": STATE.snapshot_requests(path_filter)})
            return

        # ── Auth (lib-backend/code/app/auth/router.py) ────────────────

        if path == "/v1/auth/token" and method == "POST":
            self._record_request(path, query)
            # Real backend returns 201 on success.
            self._respond_from_scenario(method, path, default_auth_response(), default_status=201)
            return
        if path == "/v1/auth/session/continue" and method == "POST":
            self._record_request(path, query)
            self._respond_from_scenario(method, path, {"status": "success"}, default_status=200)
            return
        if path == "/v1/auth/ping" and method == "GET":
            self._record_request(path, query)
            self._respond_from_scenario(method, path, {"ping": "pong!"}, default_status=200)
            return

        # ── Collect (lib-backend/code/app/collect/router.py) ──────────

        if path in ("/v1/collect/event", "/v1/collect/log", "/v1/collect/telemetry", "/v1/collect/data") and method == "POST":
            self._record_request(path, query)
            # Real backend returns 201 with no body for create endpoints; harness returns
            # {"ok": true} so tests have something to assert on if they want.
            self._respond_from_scenario(method, path, {"ok": True}, default_status=201)
            return

        # ── Storage (lib-backend/code/app/storage/router.py) ──────────

        if path == "/v1/storage/config" and method == "GET":
            self._record_request(path, query)
            self._respond_from_scenario(method, path, default_config_response(), default_status=200)
            return
        if path == "/v1/storage" and method == "GET":
            self._record_request(path, query)
            self._respond_from_scenario(method, path, default_storage_response(), default_status=200)
            return
        if path == "/v1/storage" and method == "POST":
            self._record_request(path, query)
            self._respond_from_scenario(method, path, {"status": "success"}, default_status=201)
            return
        if path == "/v1/storage" and method == "DELETE":
            self._record_request(path, query)
            self._respond_from_scenario(method, path, {"status": "all data reset"}, default_status=200)
            return

        # ── Catch-all so the harness can see misrouted requests ───────

        self._record_request(path, query)
        # Real backend would return 404 with {"detail": "Not Found"} (FastAPI default).
        self._write_json(404, {"detail": "Not Found", "path": path})


# ── Entrypoint ───────────────────────────────────────────────────────────────

def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=8765)
    args = parser.parse_args()

    server = ThreadingHTTPServer((args.host, args.port), Handler)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()
    return 0


if __name__ == "__main__":
    sys.exit(main())
