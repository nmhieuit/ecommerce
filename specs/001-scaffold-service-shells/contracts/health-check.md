# Contract: Health Check Endpoints

**Applies to**: all four services (parties, products, baskets, orders) — identical contract shape, deployed independently per service. This is the only interface this feature exposes; it is a platform/operational contract (consumed by Kubernetes probes and local developer verification), not a business API, so it is not part of the constitution's Principle II OpenAPI business-contract discipline. It is still documented here because the constitution requires liveness/readiness to be a real, checkable interface (Principle VII).

## `GET /health/live`

**Purpose**: Liveness probe — is the process running and able to respond at all.

**Request**: No parameters, no authentication (Kubernetes probes cannot present a token — documented exception, see plan.md Constitution Check).

**Response — 200 OK** (process is alive):
```json
{ "status": "Healthy" }
```

**Response — 503 Service Unavailable**: process is alive but internally deadlocked/unresponsive to the check (rare; Kubernetes restarts the pod on repeated failure).

## `GET /health/ready`

**Purpose**: Readiness probe — is the service actually able to serve requests right now, including reaching its own database.

**Request**: No parameters, no authentication (same exception as above).

**Response — 200 OK** (ready to serve traffic):
```json
{
  "status": "Healthy",
  "checks": [
    { "name": "self-database", "status": "Healthy" }
  ]
}
```

**Response — 503 Service Unavailable** (not ready — e.g., database unreachable):
```json
{
  "status": "Unhealthy",
  "checks": [
    { "name": "self-database", "status": "Unhealthy", "description": "<connection failure detail>" }
  ]
}
```

## Consumers

- **Kubernetes**: liveness probe → `/health/live` (pod restart on failure); readiness probe → `/health/ready` (traffic routing gate).
- **Local developer verification** (this feature's spec SC-001/SC-002): manually or script-checked via `curl` against a running service instance — see quickstart.md.
- **Elastic/OTel** (constitution Principle VII): probe results are not separately shipped as telemetry beyond standard request tracing — the `ServiceDefaults` component instruments the HTTP pipeline these endpoints run on like any other request.
