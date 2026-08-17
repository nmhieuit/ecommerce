# Build context is the FRONTEND WORKSPACE ROOT, not this folder — the app is a pnpm workspace
# member and needs the lockfile and the generated API client package it depends on. Note that -f is
# resolved against the working directory while the context is a separate argument, so from the
# repository root:
#
#   docker build -f frontend/apps/web/Dockerfile -t storefront ./frontend
#
# The backend origin is inlined at build time. Vite substitutes import.meta.env.* into the bundle
# when it builds, so this is fixed in the image rather than read when the container starts — and the
# correct value is the address the BROWSER can reach. The browser runs on the host, so that is the
# gateway's published host address, never the compose hostname `gateway-api:8080`, which resolves
# only inside the network (005-one-command-local-run research.md Decision 6).
ARG GATEWAY_ORIGIN=http://localhost:5300

FROM node:22-alpine AS build
WORKDIR /workspace

RUN corepack enable

# Manifests first, so the install layer is cached independently of source edits.
COPY package.json pnpm-lock.yaml pnpm-workspace.yaml .npmrc ./
COPY apps/web/package.json apps/web/
COPY packages/api-client/package.json packages/api-client/
RUN pnpm install --frozen-lockfile

COPY . .

ARG GATEWAY_ORIGIN
ENV VITE_GATEWAY_ORIGIN=${GATEWAY_ORIGIN}

# `pnpm build` typechecks and then builds; a type error fails the image rather than shipping.
RUN pnpm --filter @ecommerce/web build

FROM nginx:alpine AS final

# The history fallback lives here — see the file for why it is load-bearing rather than cosmetic.
COPY apps/web/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /workspace/apps/web/dist /usr/share/nginx/html

EXPOSE 80
