# `deploy/k8s/` — secret delivery manifests

**Feature**: [specs/018-cluster-secret-store](../../specs/018-cluster-secret-store/spec.md) | **Related ADR**: [docs/adr/0007-secrets-delivery.md](../../docs/adr/0007-secrets-delivery.md)

This directory holds **declarative manifests only** — `ExternalSecret` and example `Secret` YAML per service, structured according to [`external-secret-manifest-contract.md`](../../specs/018-cluster-secret-store/contracts/external-secret-manifest-contract.md). It does **not** provision any infrastructure.

## What exists here vs. what doesn't

- **Here**: the contract each service expects from the cluster — which secret keys it needs, what the resulting `Secret` object must be named, how often it refreshes.
- **Not here, and not in scope for `specs/018-cluster-secret-store`**: the actual self-hosted HashiCorp Vault deployment, the External Secrets Operator installation, and the `ClusterSecretStore` connecting the two. Those remain open action items on ADR-0007 — a platform-infrastructure track, provisioned via Ansible per the constitution's Technology and Infrastructure Constraints, independent of this application-level feature.

Until that infrastructure exists, these manifests are reviewable documentation of the target shape, validated structurally (see `secret.example.yaml` per service and `scripts/ci/validate-external-secrets.sh`) rather than by applying them to a production cluster. A minimal local cluster (kind/k3d) with a dev-mode Vault can be used to validate them end-to-end without touching production infrastructure — see [`quickstart.md`](../../specs/018-cluster-secret-store/quickstart.md) Kịch bản 2c and 3.

## Layout

```text
deploy/k8s/
├── README.md                    # this file
└── <service>/
    ├── external-secret.yaml     # applied to a real cluster once ESO/Vault exist
    └── secret.example.yaml      # NOT applied anywhere — placeholder values only, for review
```

## Naming contract (do not diverge without updating both sides)

| Element | Convention |
|---|---|
| `ExternalSecret` name | `<service>-secrets` |
| Target `Secret` name | `<service>-secrets` |
| `data[].secretKey` | Exact match of the `RequiredSecret.Name` declared in that service's `Program.cs` (`shared/ServiceDefaults/RequiredSecretsValidation.cs`) |

See the contract document for the full field-by-field rationale.
