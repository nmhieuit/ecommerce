# Commerce Platform — System Design

**Inputs:** [constitution.md](../.specify/memory/constitution.md) (fixed principles/stack), [docs/adr/0001–0010](adr/) and [docs/tech-stack-decisions.md](tech-stack-decisions.md) (selected products).
**Scope:** parties, products, baskets, orders, logistics, invoices services; API gateway (YARP); BFF (Minimal APIs); identity server (Duende IdentityServer); RabbitMQ/MassTransit; SQL Server (schema-per-tenant) + Redis; web + mobile-web SPAs; Elastic observability; Vault/ESO; Unleash; Pact Broker; Kubernetes/Ansible.

No technology appears below that isn't already fixed by the constitution or decided in an ADR. One gap surfaced while designing this and is called out explicitly rather than resolved here: **there is no documented decision for the OTel Collector topology** (per-node DaemonSet vs. per-service sidecar vs. gateway-only collector) — the constitution and ADRs establish *that* telemetry goes to Elastic via `ServiceDefaults`, not *how* it's collected in transit. That's a system-design-level decision this document proposes (per-node DaemonSet, noted in Diagram 2's explanation) but it hasn't been ratified as an ADR — flagging it for the platform maintainers to formalize.

---

## Diagram 1 — System Context

**What it shows:** the platform as a single box, its two tenants' users, the two client apps, the edge load balancer, and the identity provider as the one external-facing dependency users interact with directly (via OIDC redirect) alongside the platform itself.

```xml
<mxfile host="app.diagrams.net" modified="2026-08-14T00:00:00.000Z" agent="5.0" version="24.0.0" type="device">
  <diagram id="context-diagram" name="1 - System Context">
    <mxGraphModel dx="900" dy="700" grid="1" gridSize="10" guides="1" tooltips="1" connect="1" arrows="1" fold="1" page="1" pageScale="1" pageWidth="960" pageHeight="760" math="0" shadow="0">
      <root>
        <mxCell id="0" />
        <mxCell id="1" parent="0" />

        <mxCell id="n1" value="Tenant A User" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#fff2cc;strokeColor=#d6b656;fontSize=12;" vertex="1" parent="1">
          <mxGeometry x="60" y="30" width="160" height="50" as="geometry" />
        </mxCell>
        <mxCell id="n2" value="Tenant B User" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#fff2cc;strokeColor=#d6b656;fontSize=12;" vertex="1" parent="1">
          <mxGeometry x="680" y="30" width="160" height="50" as="geometry" />
        </mxCell>

        <mxCell id="n3" value="Web SPA&#10;(React + TypeScript, Vite)" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#dae8fc;strokeColor=#6c8ebf;fontSize=12;" vertex="1" parent="1">
          <mxGeometry x="40" y="140" width="200" height="70" as="geometry" />
        </mxCell>
        <mxCell id="n4" value="Mobile-Web SPA&#10;(React + TypeScript, Vite)" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#dae8fc;strokeColor=#6c8ebf;fontSize=12;" vertex="1" parent="1">
          <mxGeometry x="660" y="140" width="200" height="70" as="geometry" />
        </mxCell>

        <mxCell id="n5" value="Load Balancer" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#dae8fc;strokeColor=#6c8ebf;fontSize=12;" vertex="1" parent="1">
          <mxGeometry x="350" y="260" width="200" height="60" as="geometry" />
        </mxCell>

        <mxCell id="n6" value="Commerce Platform&#10;(API Gateway, BFF, 6 microservices, messaging, data —&#10;see Diagram 2: Container/Deployment)" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#d5e8d4;strokeColor=#82b366;fontSize=13;fontStyle=1;" vertex="1" parent="1">
          <mxGeometry x="250" y="380" width="400" height="170" as="geometry" />
        </mxCell>

        <mxCell id="n7" value="Identity Server&#10;(Duende IdentityServer)&#10;central token issuer" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#e1d5e7;strokeColor=#9673a6;fontSize=12;" vertex="1" parent="1">
          <mxGeometry x="700" y="400" width="220" height="90" as="geometry" />
        </mxCell>

        <mxCell id="e1" value="uses" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=10;" edge="1" parent="1" source="n1" target="n3">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e2" value="uses" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=10;" edge="1" parent="1" source="n2" target="n4">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e3" value="HTTPS" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=10;" edge="1" parent="1" source="n3" target="n5">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e4" value="HTTPS" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=10;" edge="1" parent="1" source="n4" target="n5">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e5" value="routed to gateway" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=10;" edge="1" parent="1" source="n5" target="n6">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e6" value="OIDC login redirect" style="edgeStyle=orthogonalEdgeStyle;dashed=1;html=1;endArrow=block;fontSize=10;" edge="1" parent="1" source="n3" target="n7">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e7" value="OIDC login redirect" style="edgeStyle=orthogonalEdgeStyle;dashed=1;html=1;endArrow=block;fontSize=10;" edge="1" parent="1" source="n4" target="n7">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e8" value="JWKS / token validation" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=10;" edge="1" parent="1" source="n6" target="n7">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>

        <mxCell id="lg0" value="Legend" style="text;html=1;fontStyle=1;fontSize=12;" vertex="1" parent="1">
          <mxGeometry x="40" y="600" width="100" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lg1" style="edgeStyle=none;html=1;endArrow=block;fontSize=10;" edge="1" parent="1">
          <mxGeometry relative="1" as="geometry">
            <mxPoint x="40" y="630" as="sourcePoint" />
            <mxPoint x="90" y="630" as="targetPoint" />
          </mxGeometry>
        </mxCell>
        <mxCell id="lg1t" value="Synchronous HTTPS request" style="text;html=1;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="100" y="620" width="220" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lg2" style="edgeStyle=none;dashed=1;html=1;endArrow=block;fontSize=10;" edge="1" parent="1">
          <mxGeometry relative="1" as="geometry">
            <mxPoint x="40" y="660" as="sourcePoint" />
            <mxPoint x="90" y="660" as="targetPoint" />
          </mxGeometry>
        </mxCell>
        <mxCell id="lg2t" value="Browser redirect (OIDC) / token check" style="text;html=1;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="100" y="650" width="260" height="20" as="geometry" />
        </mxCell>
      </root>
    </mxGraphModel>
  </diagram>
</mxfile>
```

**Constitutional grounding:** the platform is drawn as one box because at context level the internal service boundaries (Principle I) aren't the audience's concern — that's Diagram 2. The identity server appears as a distinct box even though ADR-0001 deploys it *inside* the same K8s cluster, because from a user's-eye view it is a separate interaction (a login redirect), and Principle VI requires it to be treated as independently trusted, not merged into "the platform" conceptually. Frontends talk to the load balancer, never directly to any microservice — enforced structurally by there being no edge from `n3`/`n4` to anything inside `n6` (Principle IX).

**Trade-offs / open questions:** this diagram intentionally hides the BFF-only access rule (frontends can't literally reach microservices even if they wanted to) — that's asserted by the constitution, not visually provable at this zoom level; Diagram 2 is where that constraint becomes visible as an actual absence of edges.

---

## Diagram 2 — Container / Deployment

**What it shows:** every backend container in the platform, colour-coded by role, with solid edges for synchronous HTTP calls and dashed edges for asynchronous events, telemetry, and background sync.

```xml
<mxfile host="app.diagrams.net" modified="2026-08-14T00:00:00.000Z" agent="5.0" version="24.0.0" type="device">
  <diagram id="container-diagram" name="2 - Container Deployment">
    <mxGraphModel dx="1200" dy="900" grid="1" gridSize="10" guides="1" tooltips="1" connect="1" arrows="1" page="1" pageScale="1" pageWidth="1300" pageHeight="1150" math="0" shadow="0">
      <root>
        <mxCell id="0" />
        <mxCell id="1" parent="0" />

        <mxCell id="n1" value="Load Balancer" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#dae8fc;strokeColor=#6c8ebf;fontSize=12;" vertex="1" parent="1">
          <mxGeometry x="560" y="20" width="200" height="50" as="geometry" />
        </mxCell>
        <mxCell id="n2" value="API Gateway (YARP)" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#dae8fc;strokeColor=#6c8ebf;fontSize=12;" vertex="1" parent="1">
          <mxGeometry x="560" y="110" width="200" height="50" as="geometry" />
        </mxCell>
        <mxCell id="n3" value="BFF (ASP.NET Core&#10;Minimal APIs)" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#dae8fc;strokeColor=#6c8ebf;fontSize=12;" vertex="1" parent="1">
          <mxGeometry x="560" y="200" width="200" height="60" as="geometry" />
        </mxCell>
        <mxCell id="n4" value="Identity Server&#10;(Duende IdentityServer)" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#e1d5e7;strokeColor=#9673a6;fontSize=12;" vertex="1" parent="1">
          <mxGeometry x="960" y="110" width="220" height="60" as="geometry" />
        </mxCell>

        <mxCell id="n5" value="Parties" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#d5e8d4;strokeColor=#82b366;fontSize=12;" vertex="1" parent="1">
          <mxGeometry x="40" y="330" width="150" height="60" as="geometry" />
        </mxCell>
        <mxCell id="n6" value="Products" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#d5e8d4;strokeColor=#82b366;fontSize=12;" vertex="1" parent="1">
          <mxGeometry x="210" y="330" width="150" height="60" as="geometry" />
        </mxCell>
        <mxCell id="n7" value="Baskets" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#d5e8d4;strokeColor=#82b366;fontSize=12;" vertex="1" parent="1">
          <mxGeometry x="380" y="330" width="150" height="60" as="geometry" />
        </mxCell>
        <mxCell id="n8" value="Orders" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#d5e8d4;strokeColor=#82b366;fontSize=12;" vertex="1" parent="1">
          <mxGeometry x="550" y="330" width="150" height="60" as="geometry" />
        </mxCell>
        <mxCell id="n9" value="Logistics" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#d5e8d4;strokeColor=#82b366;fontSize=12;" vertex="1" parent="1">
          <mxGeometry x="720" y="330" width="150" height="60" as="geometry" />
        </mxCell>
        <mxCell id="n10" value="Invoices" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#d5e8d4;strokeColor=#82b366;fontSize=12;" vertex="1" parent="1">
          <mxGeometry x="890" y="330" width="150" height="60" as="geometry" />
        </mxCell>

        <mxCell id="n11" value="SQL Server&#10;(parties, schema-per-tenant)" style="shape=cylinder;whiteSpace=wrap;html=1;fillColor=#fff2cc;strokeColor=#d6b656;fontSize=10;boundedLbl=1;" vertex="1" parent="1">
          <mxGeometry x="40" y="430" width="150" height="70" as="geometry" />
        </mxCell>
        <mxCell id="n12" value="SQL Server&#10;(products, schema-per-tenant)" style="shape=cylinder;whiteSpace=wrap;html=1;fillColor=#fff2cc;strokeColor=#d6b656;fontSize=10;boundedLbl=1;" vertex="1" parent="1">
          <mxGeometry x="210" y="430" width="150" height="70" as="geometry" />
        </mxCell>
        <mxCell id="n13" value="SQL Server&#10;(baskets, schema-per-tenant)" style="shape=cylinder;whiteSpace=wrap;html=1;fillColor=#fff2cc;strokeColor=#d6b656;fontSize=10;boundedLbl=1;" vertex="1" parent="1">
          <mxGeometry x="380" y="430" width="150" height="70" as="geometry" />
        </mxCell>
        <mxCell id="n14" value="SQL Server&#10;(orders, schema-per-tenant)" style="shape=cylinder;whiteSpace=wrap;html=1;fillColor=#fff2cc;strokeColor=#d6b656;fontSize=10;boundedLbl=1;" vertex="1" parent="1">
          <mxGeometry x="550" y="430" width="150" height="70" as="geometry" />
        </mxCell>
        <mxCell id="n15" value="SQL Server&#10;(logistics, schema-per-tenant)" style="shape=cylinder;whiteSpace=wrap;html=1;fillColor=#fff2cc;strokeColor=#d6b656;fontSize=10;boundedLbl=1;" vertex="1" parent="1">
          <mxGeometry x="720" y="430" width="150" height="70" as="geometry" />
        </mxCell>
        <mxCell id="n16" value="SQL Server&#10;(invoices, schema-per-tenant)" style="shape=cylinder;whiteSpace=wrap;html=1;fillColor=#fff2cc;strokeColor=#d6b656;fontSize=10;boundedLbl=1;" vertex="1" parent="1">
          <mxGeometry x="890" y="430" width="150" height="70" as="geometry" />
        </mxCell>

        <mxCell id="n17" value="Redis&#10;(basket store + cache)" style="shape=cylinder;whiteSpace=wrap;html=1;fillColor=#fff2cc;strokeColor=#d6b656;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="380" y="540" width="150" height="60" as="geometry" />
        </mxCell>

        <mxCell id="n18" value="RabbitMQ (MassTransit)&#10;outbox + retry/DLQ policies" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#ffe6cc;strokeColor=#d79b00;fontSize=12;" vertex="1" parent="1">
          <mxGeometry x="380" y="640" width="300" height="60" as="geometry" />
        </mxCell>

        <mxCell id="n19" value="HashiCorp Vault" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#e1d5e7;strokeColor=#9673a6;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="40" y="760" width="160" height="60" as="geometry" />
        </mxCell>
        <mxCell id="n20" value="External Secrets&#10;Operator" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#e1d5e7;strokeColor=#9673a6;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="230" y="760" width="160" height="60" as="geometry" />
        </mxCell>
        <mxCell id="n21" value="Unleash&#10;(feature toggles)" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#666666;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="420" y="760" width="160" height="60" as="geometry" />
        </mxCell>
        <mxCell id="n22" value="Pact Broker&#10;(contract tests)" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#666666;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="610" y="760" width="160" height="60" as="geometry" />
        </mxCell>
        <mxCell id="n23" value="Elastic Stack&#10;(OTel Collector → Elasticsearch / Kibana)" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#666666;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="890" y="760" width="220" height="60" as="geometry" />
        </mxCell>

        <mxCell id="e1" value="HTTPS" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=9;" edge="1" parent="1" source="n1" target="n2">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e2" value="HTTPS (validated JWT)" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=9;" edge="1" parent="1" source="n2" target="n3">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e3" value="JWKS" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=9;" edge="1" parent="1" source="n2" target="n4">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e4" value="independent JWT validation" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=9;" edge="1" parent="1" source="n3" target="n4">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>

        <mxCell id="e5" value="HTTPS" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=9;" edge="1" parent="1" source="n3" target="n5">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e6" value="HTTPS" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=9;" edge="1" parent="1" source="n3" target="n6">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e7" value="HTTPS" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=9;" edge="1" parent="1" source="n3" target="n7">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e8" value="HTTPS" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=9;" edge="1" parent="1" source="n3" target="n8">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e9" value="HTTPS" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=9;" edge="1" parent="1" source="n3" target="n9">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e10" value="HTTPS" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=9;" edge="1" parent="1" source="n3" target="n10">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>

        <mxCell id="e11" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=9;" edge="1" parent="1" source="n5" target="n11">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e12" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=9;" edge="1" parent="1" source="n6" target="n12">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e13" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=9;" edge="1" parent="1" source="n7" target="n13">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e14" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=9;" edge="1" parent="1" source="n8" target="n14">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e15" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=9;" edge="1" parent="1" source="n9" target="n15">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e16" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=9;" edge="1" parent="1" source="n10" target="n16">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e17" value="EF Core" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=9;" edge="1" parent="1" source="n7" target="n17">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>

        <mxCell id="e18" value="publish BasketCheckedOut" style="edgeStyle=orthogonalEdgeStyle;dashed=1;html=1;endArrow=block;fontSize=9;" edge="1" parent="1" source="n7" target="n18">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e19" value="publish OrderPlaced (outbox)" style="edgeStyle=orthogonalEdgeStyle;dashed=1;html=1;endArrow=block;fontSize=9;" edge="1" parent="1" source="n8" target="n18">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e20" value="consume OrderPlaced" style="edgeStyle=orthogonalEdgeStyle;dashed=1;html=1;endArrow=block;fontSize=9;" edge="1" parent="1" source="n18" target="n9">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e21" value="consume OrderPlaced" style="edgeStyle=orthogonalEdgeStyle;dashed=1;html=1;endArrow=block;fontSize=9;" edge="1" parent="1" source="n18" target="n10">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>

        <mxCell id="e22" value="sync secrets" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=9;" edge="1" parent="1" source="n20" target="n19">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e23" value="flag evaluation SDK&#10;(+ every service &amp; frontend)" style="edgeStyle=orthogonalEdgeStyle;dashed=1;html=1;endArrow=block;fontSize=9;" edge="1" parent="1" source="n21" target="n3">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e24" value="CI-time contract verification&#10;(every HTTP/event boundary)" style="edgeStyle=orthogonalEdgeStyle;dashed=1;html=1;endArrow=block;fontSize=9;" edge="1" parent="1" source="n22" target="n8">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>

        <mxCell id="e25" value="OTel traces/metrics/logs" style="edgeStyle=orthogonalEdgeStyle;dashed=1;html=1;endArrow=block;fontSize=9;" edge="1" parent="1" source="n2" target="n23">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e26" value="OTel traces/metrics/logs" style="edgeStyle=orthogonalEdgeStyle;dashed=1;html=1;endArrow=block;fontSize=9;" edge="1" parent="1" source="n3" target="n23">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e27" value="OTel traces/metrics/logs" style="edgeStyle=orthogonalEdgeStyle;dashed=1;html=1;endArrow=block;fontSize=9;" edge="1" parent="1" source="n4" target="n23">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e28" value="OTel (+ all 6 services&#10;via shared ServiceDefaults)" style="edgeStyle=orthogonalEdgeStyle;dashed=1;html=1;endArrow=block;fontSize=9;" edge="1" parent="1" source="n8" target="n23">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>

        <mxCell id="lg0" value="Legend" style="text;html=1;fontStyle=1;fontSize=12;" vertex="1" parent="1">
          <mxGeometry x="40" y="1010" width="100" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#dae8fc;strokeColor=#6c8ebf;" vertex="1" parent="1">
          <mxGeometry x="40" y="1040" width="20" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc1t" value="Edge (gateway / BFF)" style="text;html=1;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="65" y="1040" width="180" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc2" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#d5e8d4;strokeColor=#82b366;" vertex="1" parent="1">
          <mxGeometry x="260" y="1040" width="20" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc2t" value="Microservice" style="text;html=1;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="285" y="1040" width="140" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc3" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#fff2cc;strokeColor=#d6b656;" vertex="1" parent="1">
          <mxGeometry x="440" y="1040" width="20" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc3t" value="Data store" style="text;html=1;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="465" y="1040" width="120" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc4" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#ffe6cc;strokeColor=#d79b00;" vertex="1" parent="1">
          <mxGeometry x="600" y="1040" width="20" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc4t" value="Messaging" style="text;html=1;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="625" y="1040" width="120" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc5" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#e1d5e7;strokeColor=#9673a6;" vertex="1" parent="1">
          <mxGeometry x="760" y="1040" width="20" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc5t" value="Identity / secrets" style="text;html=1;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="785" y="1040" width="140" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc6" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#666666;" vertex="1" parent="1">
          <mxGeometry x="940" y="1040" width="20" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc6t" value="Ops / support tooling" style="text;html=1;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="965" y="1040" width="160" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgline1" style="edgeStyle=none;html=1;endArrow=block;fontSize=9;" edge="1" parent="1">
          <mxGeometry relative="1" as="geometry">
            <mxPoint x="40" y="1080" as="sourcePoint" />
            <mxPoint x="90" y="1080" as="targetPoint" />
          </mxGeometry>
        </mxCell>
        <mxCell id="lgline1t" value="Synchronous (HTTP)" style="text;html=1;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="100" y="1070" width="180" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgline2" style="edgeStyle=none;dashed=1;html=1;endArrow=block;fontSize=9;" edge="1" parent="1">
          <mxGeometry relative="1" as="geometry">
            <mxPoint x="320" y="1080" as="sourcePoint" />
            <mxPoint x="370" y="1080" as="targetPoint" />
          </mxGeometry>
        </mxCell>
        <mxCell id="lgline2t" value="Asynchronous (event / telemetry / sync)" style="text;html=1;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="380" y="1070" width="300" height="20" as="geometry" />
        </mxCell>
      </root>
    </mxGraphModel>
  </diagram>
</mxfile>
```

**Constitutional grounding:**
- **Principle I (Service Autonomy):** each service box has exactly one database cylinder underneath it, and no edges connect one service's database to another service. Cross-service reads never happen through data — only through the BFF's HTTP calls or RabbitMQ events.
- **Principle IV (Event-Driven by Default):** Orders and Baskets publish to RabbitMQ (dashed), Logistics and Invoices consume from it — synchronous edges exist only between the edge layer and services (BFF aggregating), never service-to-service.
- **Principle V (Tenant Isolation):** every SQL Server box is explicitly labeled schema-per-tenant; there's deliberately no single shared "tenant DB" node.
- **Principle VI (Secure by Default):** both the gateway *and* the BFF have independent edges to the identity server for JWT validation — the gateway is drawn as unable to be the sole trust boundary.
- **Principle VII (Observable by Default):** the OTel edges converge on one Elastic box; the label on `e28` notes this applies uniformly to all six services via `ServiceDefaults`, not just the one drawn (drawing all 9 would clutter the diagram without adding information).

**Trade-offs / open questions:**
- The OTel Collector's own deployment topology (DaemonSet vs. sidecar vs. gateway-only) isn't decided anywhere in the ADRs — proposed here as a per-node DaemonSet (lowest per-pod overhead, one collector per K8s node rather than one per service), but this needs to be ratified as its own ADR before implementation.
- Unleash and Pact Broker edges are drawn as single representative connections (to BFF and Orders respectively) to keep the diagram legible — in reality every service integrates the Unleash SDK and every HTTP/event boundary has a Pact contract, as noted in each edge's label.

---

## Diagram 3 — Sequence: Order Placement

**What it shows:** the full path of a checkout request from the client through to asynchronous fulfillment, including tenant/correlation header propagation, the transactional outbox, and the compensation path on downstream failure.

```xml
<mxfile host="app.diagrams.net" modified="2026-08-14T00:00:00.000Z" agent="5.0" version="24.0.0" type="device">
  <diagram id="sequence-diagram" name="3 - Order Placement Sequence">
    <mxGraphModel dx="1400" dy="900" grid="1" gridSize="10" guides="1" tooltips="1" connect="1" arrows="1" page="1" pageScale="1" pageWidth="1560" pageHeight="820" math="0" shadow="0">
      <root>
        <mxCell id="0" />
        <mxCell id="1" parent="0" />

        <mxCell id="p1" value="Client&#10;(Web/Mobile SPA)" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#dae8fc;strokeColor=#6c8ebf;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="40" y="20" width="140" height="50" as="geometry" />
        </mxCell>
        <mxCell id="p2" value="API Gateway&#10;(YARP)" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#dae8fc;strokeColor=#6c8ebf;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="220" y="20" width="140" height="50" as="geometry" />
        </mxCell>
        <mxCell id="p3" value="BFF" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#dae8fc;strokeColor=#6c8ebf;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="420" y="20" width="140" height="50" as="geometry" />
        </mxCell>
        <mxCell id="p4" value="Baskets&#10;Service" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#d5e8d4;strokeColor=#82b366;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="620" y="20" width="140" height="50" as="geometry" />
        </mxCell>
        <mxCell id="p5" value="Orders&#10;Service" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#d5e8d4;strokeColor=#82b366;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="820" y="20" width="140" height="50" as="geometry" />
        </mxCell>
        <mxCell id="p6" value="RabbitMQ" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#ffe6cc;strokeColor=#d79b00;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="1020" y="20" width="140" height="50" as="geometry" />
        </mxCell>
        <mxCell id="p7" value="Logistics&#10;Service" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#d5e8d4;strokeColor=#82b366;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="1220" y="20" width="140" height="50" as="geometry" />
        </mxCell>
        <mxCell id="p8" value="Invoices&#10;Service" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#d5e8d4;strokeColor=#82b366;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="1400" y="20" width="140" height="50" as="geometry" />
        </mxCell>

        <mxCell id="l1" style="html=1;dashed=1;endArrow=none;startArrow=none;" edge="1" parent="1">
          <mxGeometry relative="1" as="geometry"><mxPoint x="110" y="90" as="sourcePoint" /><mxPoint x="110" y="750" as="targetPoint" /></mxGeometry>
        </mxCell>
        <mxCell id="l2" style="html=1;dashed=1;endArrow=none;startArrow=none;" edge="1" parent="1">
          <mxGeometry relative="1" as="geometry"><mxPoint x="290" y="90" as="sourcePoint" /><mxPoint x="290" y="750" as="targetPoint" /></mxGeometry>
        </mxCell>
        <mxCell id="l3" style="html=1;dashed=1;endArrow=none;startArrow=none;" edge="1" parent="1">
          <mxGeometry relative="1" as="geometry"><mxPoint x="490" y="90" as="sourcePoint" /><mxPoint x="490" y="750" as="targetPoint" /></mxGeometry>
        </mxCell>
        <mxCell id="l4" style="html=1;dashed=1;endArrow=none;startArrow=none;" edge="1" parent="1">
          <mxGeometry relative="1" as="geometry"><mxPoint x="690" y="90" as="sourcePoint" /><mxPoint x="690" y="750" as="targetPoint" /></mxGeometry>
        </mxCell>
        <mxCell id="l5" style="html=1;dashed=1;endArrow=none;startArrow=none;" edge="1" parent="1">
          <mxGeometry relative="1" as="geometry"><mxPoint x="890" y="90" as="sourcePoint" /><mxPoint x="890" y="750" as="targetPoint" /></mxGeometry>
        </mxCell>
        <mxCell id="l6" style="html=1;dashed=1;endArrow=none;startArrow=none;" edge="1" parent="1">
          <mxGeometry relative="1" as="geometry"><mxPoint x="1090" y="90" as="sourcePoint" /><mxPoint x="1090" y="750" as="targetPoint" /></mxGeometry>
        </mxCell>
        <mxCell id="l7" style="html=1;dashed=1;endArrow=none;startArrow=none;" edge="1" parent="1">
          <mxGeometry relative="1" as="geometry"><mxPoint x="1290" y="90" as="sourcePoint" /><mxPoint x="1290" y="750" as="targetPoint" /></mxGeometry>
        </mxCell>
        <mxCell id="l8" style="html=1;dashed=1;endArrow=none;startArrow=none;" edge="1" parent="1">
          <mxGeometry relative="1" as="geometry"><mxPoint x="1470" y="90" as="sourcePoint" /><mxPoint x="1470" y="750" as="targetPoint" /></mxGeometry>
        </mxCell>

        <mxCell id="s1" value="1. POST /checkout (Bearer JWT)" style="html=1;endArrow=block;fontSize=10;" edge="1" parent="1">
          <mxGeometry relative="1" as="geometry"><mxPoint x="110" y="110" as="sourcePoint" /><mxPoint x="290" y="110" as="targetPoint" /></mxGeometry>
        </mxCell>
        <mxCell id="note1" value="Gateway validates JWT independently; extracts tenant claim; generates X-Correlation-Id (Principles V, VI, VII)" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#ffffff;strokeColor=#999999;fontSize=9;dashed=1;" vertex="1" parent="1">
          <mxGeometry x="320" y="130" width="260" height="40" as="geometry" />
        </mxCell>
        <mxCell id="s2" value="2. POST /checkout (X-Tenant-Id, X-Correlation-Id)" style="html=1;endArrow=block;fontSize=10;" edge="1" parent="1">
          <mxGeometry relative="1" as="geometry"><mxPoint x="290" y="190" as="sourcePoint" /><mxPoint x="490" y="190" as="targetPoint" /></mxGeometry>
        </mxCell>
        <mxCell id="s3" value="3. GET /baskets/{id} (tenant + correlation headers)" style="html=1;endArrow=block;fontSize=10;" edge="1" parent="1">
          <mxGeometry relative="1" as="geometry"><mxPoint x="490" y="230" as="sourcePoint" /><mxPoint x="690" y="230" as="targetPoint" /></mxGeometry>
        </mxCell>
        <mxCell id="s4" value="4. basket snapshot" style="html=1;dashed=1;endArrow=block;fontSize=10;" edge="1" parent="1">
          <mxGeometry relative="1" as="geometry"><mxPoint x="690" y="265" as="sourcePoint" /><mxPoint x="490" y="265" as="targetPoint" /></mxGeometry>
        </mxCell>
        <mxCell id="s5" value="5. POST /orders (tenant + correlation headers, basket snapshot)" style="html=1;endArrow=block;fontSize=10;" edge="1" parent="1">
          <mxGeometry relative="1" as="geometry"><mxPoint x="490" y="305" as="sourcePoint" /><mxPoint x="890" y="305" as="targetPoint" /></mxGeometry>
        </mxCell>
        <mxCell id="note2" value="Orders writes the Order row and the Outbox row in one DB transaction — state change and event publication cannot diverge (Principle IV)" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#ffffff;strokeColor=#999999;fontSize=9;dashed=1;" vertex="1" parent="1">
          <mxGeometry x="920" y="325" width="280" height="45" as="geometry" />
        </mxCell>
        <mxCell id="s6" value="6. 201 Created { orderId }" style="html=1;dashed=1;endArrow=block;fontSize=10;" edge="1" parent="1">
          <mxGeometry relative="1" as="geometry"><mxPoint x="890" y="390" as="sourcePoint" /><mxPoint x="490" y="390" as="targetPoint" /></mxGeometry>
        </mxCell>
        <mxCell id="s7" value="7. 201 Created" style="html=1;dashed=1;endArrow=block;fontSize=10;" edge="1" parent="1">
          <mxGeometry relative="1" as="geometry"><mxPoint x="490" y="420" as="sourcePoint" /><mxPoint x="290" y="420" as="targetPoint" /></mxGeometry>
        </mxCell>
        <mxCell id="s8" value="8. 201 Created (checkout complete)" style="html=1;dashed=1;endArrow=block;fontSize=10;" edge="1" parent="1">
          <mxGeometry relative="1" as="geometry"><mxPoint x="290" y="450" as="sourcePoint" /><mxPoint x="110" y="450" as="targetPoint" /></mxGeometry>
        </mxCell>
        <mxCell id="note3" value="Outbox relay polls &amp; publishes asynchronously — decoupled from the HTTP response the client already received above" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#ffffff;strokeColor=#999999;fontSize=9;dashed=1;" vertex="1" parent="1">
          <mxGeometry x="920" y="480" width="280" height="45" as="geometry" />
        </mxCell>
        <mxCell id="s9" value="9. publish OrderPlaced v1 (via outbox relay)" style="html=1;dashed=1;endArrow=block;fontSize=10;" edge="1" parent="1">
          <mxGeometry relative="1" as="geometry"><mxPoint x="890" y="540" as="sourcePoint" /><mxPoint x="1090" y="540" as="targetPoint" /></mxGeometry>
        </mxCell>
        <mxCell id="s10" value="10. consume OrderPlaced (idempotent handler)" style="html=1;dashed=1;endArrow=block;fontSize=10;" edge="1" parent="1">
          <mxGeometry relative="1" as="geometry"><mxPoint x="1090" y="580" as="sourcePoint" /><mxPoint x="1290" y="580" as="targetPoint" /></mxGeometry>
        </mxCell>
        <mxCell id="s11" value="11. consume OrderPlaced (idempotent handler)" style="html=1;dashed=1;endArrow=block;fontSize=10;" edge="1" parent="1">
          <mxGeometry relative="1" as="geometry"><mxPoint x="1090" y="615" as="sourcePoint" /><mxPoint x="1470" y="615" as="targetPoint" /></mxGeometry>
        </mxCell>
        <mxCell id="s12" value="12. publish ShipmentScheduled | ShipmentFailed" style="html=1;dashed=1;endArrow=block;fontSize=10;" edge="1" parent="1">
          <mxGeometry relative="1" as="geometry"><mxPoint x="1290" y="650" as="sourcePoint" /><mxPoint x="1090" y="650" as="targetPoint" /></mxGeometry>
        </mxCell>
        <mxCell id="s13" value="13. publish InvoiceIssued | InvoiceFailed" style="html=1;dashed=1;endArrow=block;fontSize=10;" edge="1" parent="1">
          <mxGeometry relative="1" as="geometry"><mxPoint x="1470" y="685" as="sourcePoint" /><mxPoint x="1090" y="685" as="targetPoint" /></mxGeometry>
        </mxCell>
        <mxCell id="s14" value="14. compensation event on failure (e.g. ShipmentFailed) — saga transitions order state; never a distributed transaction" style="html=1;dashed=1;endArrow=block;fontSize=10;" edge="1" parent="1">
          <mxGeometry relative="1" as="geometry"><mxPoint x="1090" y="725" as="sourcePoint" /><mxPoint x="890" y="725" as="targetPoint" /></mxGeometry>
        </mxCell>

        <mxCell id="lg0" value="Legend:  solid = synchronous request   |   dashed = async return / event / compensation" style="text;html=1;fontSize=11;fontStyle=1;" vertex="1" parent="1">
          <mxGeometry x="40" y="770" width="700" height="20" as="geometry" />
        </mxCell>
      </root>
    </mxGraphModel>
  </diagram>
</mxfile>
```

**Constitutional grounding:**
- **Principle V (Tenant Isolation):** `X-Tenant-Id` appears on every synchronous hop from step 2 onward — resolved once at the gateway (from the verified token claim) and never re-derived from the request body. There's no arrow anywhere in this diagram sourcing a tenant ID from a client-supplied field.
- **Principle VII (Observable by Default):** `X-Correlation-Id` is generated at the gateway (step 1's note) and threaded through every subsequent call and event, including the async fan-out — this is what makes the whole flow traceable as one unit in Elastic later.
- **Principle IV (Event-Driven by Default):** step 5's note is the load-bearing detail — the Order row and Outbox row commit in one transaction, so the `OrderPlaced` publish (step 9) can never be lost or duplicated relative to the order actually existing. Steps 10–11 are explicitly labeled idempotent, and step 14 shows compensation via a new event rather than a distributed transaction reaching back into Orders' database.
- **Principle VIII (Performance):** the client receives its response at step 8 *before* fulfillment happens — checkout latency is bounded by the synchronous chain only (gateway→BFF→baskets→orders), not by logistics/invoices processing time.

**Trade-offs / open questions:**
- This diagram shows the happy path plus one generic failure/compensation edge (step 14); it doesn't enumerate every possible failure mode (e.g., partial fulfillment where Logistics succeeds but Invoices fails) — that belongs in the Orders service's saga state machine design, not this diagram.
- The outbox relay's polling interval (and therefore the real-world gap between step 8 and step 9) isn't specified anywhere in the constitution beyond "events processed within 5s of publication at p95" (Principle VIII) — that budget constrains the relay's polling frequency but the actual number needs to be set during implementation.

---

## Summary of Flagged Gaps

| Gap | Where it surfaced | Recommended next step |
|---|---|---|
| OTel Collector deployment topology (DaemonSet vs. sidecar vs. gateway-only) | Diagram 2 | Write ADR-0011 once the platform team picks one; this document assumes DaemonSet |
| Outbox relay polling interval | Diagram 3 | Set during Orders service implementation, bounded by the 5s p95 event-processing budget (Principle VIII) |
| Saga failure-mode enumeration beyond the generic compensation path | Diagram 3 | Belongs in the Orders service's own saga design, not this system-design document |
