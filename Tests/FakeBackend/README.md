# AbxrLib Fake Backend

A small HTTP server (stdlib only — no pip dependencies) that mimics the AbxrLib
REST endpoints so Runtime tests can run authentication and data flows
end-to-end without a real backend.

The wire format mirrors the real FastAPI backend at `lib-backend`:
- camelCase field names
- `201 Created` for POST endpoints that create resources, `200 OK` for GET/DELETE
- Error bodies use FastAPI's default shape: `{"detail": "..."}`

The C# test harness boots this script automatically — you don't normally run it
by hand. Run it manually only when iterating on the server itself.

## Requirements

Python 3.8+ on PATH. That's it — no pip, no venv, no requirements.

The C# harness looks for the Python interpreter via the `ABXR_TEST_PYTHON`
environment variable, then falls back to `python3` and `python` on PATH. You
only need to set `ABXR_TEST_PYTHON` if the default PATH lookup picks up a
Python you don't want.

## Running by hand

```bash
python server.py --port 8765
```

Then poke it:

```bash
curl http://127.0.0.1:8765/__control/health
curl -X POST http://127.0.0.1:8765/__control/reset -H 'Content-Type: application/json' -d '{}'
curl http://127.0.0.1:8765/v1/storage/config
```

## Endpoints

### Control plane (never recorded)

| Endpoint                  | Purpose                                        |
| ------------------------- | ---------------------------------------------- |
| `GET  /__control/health`  | Liveness check (used by harness on startup).   |
| `POST /__control/reset`   | Clear queued scenarios and recorded requests.  |
| `POST /__control/scenario`| Queue a response for an endpoint.              |
| `GET  /__control/requests`| Inspect every request that has arrived.        |

### Mirrored AbxrLib endpoints

| Endpoint                        | Default status | Default body                                                    |
| ------------------------------- | -------------- | --------------------------------------------------------------- |
| `POST /v1/auth/token`           | `201`          | Valid `AuthenticationResponseSchema` (camelCase, JWT token).    |
| `POST /v1/auth/session/continue`| `200`          | `{"status": "success"}`                                         |
| `GET  /v1/auth/ping`            | `200`          | `{"ping": "pong!"}`                                             |
| `POST /v1/collect/event`        | `201`          | `{"ok": true}`                                                  |
| `POST /v1/collect/log`          | `201`          | `{"ok": true}`                                                  |
| `POST /v1/collect/telemetry`    | `201`          | `{"ok": true}`                                                  |
| `POST /v1/collect/data`         | `201`          | `{"ok": true}`                                                  |
| `GET  /v1/storage`              | `200`          | `{"data": []}`                                                  |
| `POST /v1/storage`              | `201`          | `{"status": "success"}`                                         |
| `DELETE /v1/storage`            | `200`          | `{"status": "all data reset"}`                                  |
| `GET  /v1/storage/config`       | `200`          | `{}`                                                            |

Anything else returns `404 {"detail": "Not Found", "path": "..."}` and is still
recorded so the harness can see misrouted requests.

## Queuing a scenario

`POST /__control/scenario` body:

```json
{
  "method": "POST",
  "path": "/v1/auth/token",
  "status": 401,
  "body": {"detail": "Invalid AppToken"},
  "delay_ms": 0
}
```

Multiple scenarios for the same `(method, path)` queue up and are consumed in
order. The last queued scenario is sticky once the queue empties, so you can
configure a single response and have it apply to every retry.

### Behavior rules

- **`status` omitted** → endpoint's default status (e.g. `201` for `/v1/auth/token`).
- **`body` omitted** → endpoint's default body.
- **`body: null`** → empty response (status only, no body bytes).
- **`raw: "..."`** → returns the raw string as-is. Use `content_type` to set
  the response type. Useful for malformed-JSON tests:
  ```json
  { "path": "/v1/auth/token", "status": 200, "raw": "not json", "content_type": "text/plain" }
  ```

## Auth fidelity notes

The mirrored auth endpoint matches the real backend's wire format but doesn't
enforce its validation. Specifically:

- The fake accepts any request body, including missing or malformed credentials.
  The real backend uses Pydantic and returns `422` for shape violations or `401`
  for invalid tokens. If you want to test those paths, queue them explicitly with
  `/__control/scenario`.
- The fake doesn't enforce the `x-abxrlib-timestamp` / `x-abxrlib-hash` HMAC
  headers that the real backend requires on authenticated endpoints. So a test
  that passes here might still fail against the real backend if Unity's signing
  is wrong — those signing paths need to be tested separately.
