# The Jenkins controller for the local CI stack, carrying the toolchain the repository-root
# Jenkinsfile actually needs (specs/012-sonarqube-quality-gate).
#
# The stock jenkins/jenkins image ships git and a JDK and nothing else, so every stage of the
# pipeline failed on its first command with `dotnet: not found`. The Jenkinsfile documents its
# agent requirements in its own header — a POSIX shell, the .NET 10 SDK, Node 22 with corepack,
# and a reachable Docker daemon — and this image is what satisfies them.
#
# Controller-as-agent is a local-development shortcut, not a pattern to carry into production: a
# real deployment runs builds on separate agents so a build cannot reach the controller's own
# state. See T028 for the production posture.

FROM jenkins/jenkins:lts-jdk17

USER root

ARG DOTNET_VERSION=10.0
ARG NODE_MAJOR=22

# Docker CLI only — the daemon is the host's, reached through the socket mounted by
# docker-compose.ci.yml. The integration tier (spec 010) starts SQL Server, Redis and RabbitMQ
# with Testcontainers and cannot run without it.
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        ca-certificates curl gnupg libicu-dev \
    && install -m 0755 -d /etc/apt/keyrings \
    && curl -fsSL https://download.docker.com/linux/debian/gpg \
        | gpg --dearmor -o /etc/apt/keyrings/docker.gpg \
    && chmod a+r /etc/apt/keyrings/docker.gpg \
    && echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] \
https://download.docker.com/linux/debian $(. /etc/os-release && echo "$VERSION_CODENAME") stable" \
        > /etc/apt/sources.list.d/docker.list \
    && curl -fsSL https://deb.nodesource.com/setup_${NODE_MAJOR}.x | bash - \
    && apt-get update \
    && apt-get install -y --no-install-recommends docker-ce-cli nodejs \
    && rm -rf /var/lib/apt/lists/*

# The .NET SDK from the official install script rather than apt: the apt feed lags, and this
# feature needs 10.0 specifically (every project targets net10.0).
ENV DOTNET_ROOT=/usr/share/dotnet
ENV PATH=$PATH:/usr/share/dotnet:/root/.dotnet/tools
RUN curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh \
    && chmod +x /tmp/dotnet-install.sh \
    && /tmp/dotnet-install.sh --channel "${DOTNET_VERSION}" --install-dir "${DOTNET_ROOT}" \
    && rm /tmp/dotnet-install.sh \
    && ln -s "${DOTNET_ROOT}/dotnet" /usr/local/bin/dotnet

# corepack ships with Node 22 but is opt-in; the Jenkinsfile calls `corepack enable pnpm`, and
# the frontend workspace pins its pnpm version in package.json.
RUN corepack enable

# Telemetry off and first-run experience skipped: both write to the home directory on first
# invocation and add noise to every build log.
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1 \
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

# Deliberately staying root for the local stack: the mounted Docker socket is root-owned on
# Docker Desktop, and the alternative (matching the host's docker group id into the image) is not
# portable across the machines this stack is meant to run on. A production controller must not do
# this — constitution Principle VI.
USER root
