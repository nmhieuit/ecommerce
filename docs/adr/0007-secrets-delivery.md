# ADR-0007: Secrets Delivery Into the Cluster

**Status:** Accepted
**Date:** 2026-08-14
**Deciders:** Platform maintainers

## Context

Principle VI requires secrets to be injected at runtime from the cluster secret store, never baked into images, source, or config files. The platform is self-hosted K8s provisioned by Ansible, not tied to a specific cloud provider's KMS.

## Decision

Use **External Secrets Operator (ESO)**, backed by a self-hosted **HashiCorp Vault** instance as the source of truth.

## Options Considered

### Option A: External Secrets Operator + Vault
| Dimension | Assessment |
|---|---|
| Complexity | Medium — two components, but a well-understood pairing |
| Cost | Operational cost of running Vault HA |
| Scalability | High |
| Team familiarity | Low initially |

**Pros:** Application manifests reference plain, well-understood K8s Secret objects — pods don't need native Vault client/sidecar integration; ESO handles sync/rotation against Vault; Vault provides real security properties (dynamic, short-lived credentials for SQL Server/RabbitMQ, per-tenant/per-service access policy, audit logging) that map directly onto Principle V's per-tenant isolation model; decouples "where secrets live" from "how pods consume them," so the backing store could change later without touching application manifests.
**Cons:** Two systems to run and keep available; Vault itself needs correct HA setup, unsealing, storage backend, and backup/DR — genuine new operational burden for an Ansible/K8s team.

### Option B: HashiCorp Vault + Agent Injector/CSI driver (no ESO)
| Dimension | Assessment |
|---|---|
| Complexity | High — every pod needs Vault-aware sidecar/init config |
| Cost | Same Vault operational cost as Option A |
| Scalability | High |
| Team familiarity | Low |

**Pros:** Same Vault security properties as Option A, with secrets never landing as static K8s Secret objects at all (injected directly into the pod filesystem/env at runtime).
**Cons:** Every single service's deployment manifest needs Vault-specific annotations/sidecar config — more per-service coupling to Vault than ESO's "just a normal K8s Secret" model, and higher review burden every time a new service is added.

### Option C: K8s-native secrets only, no external backing store
| Dimension | Assessment |
|---|---|
| Complexity | Low |
| Cost | None |
| Scalability | High |
| Team familiarity | High |

**Pros:** Simplest possible setup — nothing new to run.
**Cons:** No dynamic/short-lived credentials, no centralized audit trail of secret access, no rotation mechanism beyond manual updates; secrets at rest are only as safe as etcd encryption, which is a separate concern to get right; doesn't meaningfully improve on "secrets injected at runtime" beyond the bare minimum the constitution requires.

## Trade-off Analysis

Option C technically satisfies the constitution's letter but not its spirit — "injected at runtime" without rotation or audit is a weak security posture for a system handling PII and payment-adjacent data (Principle VI also mandates PII encryption and OWASP mitigations). Between the two Vault-backed options, ESO is chosen because it keeps every application manifest simple and Vault-agnostic, containing Vault-specific complexity to one operator rather than spreading it across every service's deployment config.

## Consequences

- Vault must be deployed with real HA and backup/DR from day one — deferring this and migrating later is expensive.
- Secret rotation policy (which secrets are dynamic vs. static) needs to be defined per dependency type (SQL Server, RabbitMQ, identity server signing keys).

## Action Items

1. [ ] Deploy self-hosted Vault with HA storage backend via Ansible
2. [ ] Install ESO and define the first `SecretStore`/`ExternalSecret` for one service as a pilot
3. [ ] Define dynamic-credential policies for SQL Server and RabbitMQ access
