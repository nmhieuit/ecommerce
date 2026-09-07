# Jenkins controller image with the toolchain the Jenkinsfile's agent requires: a POSIX shell,
# the .NET 10 SDK, Node 22 with corepack/pnpm, the Docker CLI (talking to the host daemon via the
# mounted socket — see docker-compose.ci.yml), and git.
#
# This reconstructs an image that previously existed only as a manually-built, untagged local
# image (`ecomerce-ci-jenkins:local`) — reproducible nowhere else and silently lost the moment the
# container was recreated from docker-compose.ci.yml's plain `jenkins/jenkins:lts-jdk17`. Captured
# from that image's actual build history (`docker history ecomerce-ci-jenkins:local --no-trunc`)
# after diagnosing a `dotnet: not found` failure caused by exactly that loss.
#
# gitleaks and Trivy (specs/018-cluster-secret-store) are installed as pinned static binaries
# rather than through apt — neither ships a Debian package in the base image's repositories, and a
# pinned release download keeps the two new CI stages (ci/secret-scan, ci/image-secret-scan;
# contracts/ci-secret-scan-stage-contract.md) reproducible the same way the .NET SDK install below
# already is.
FROM jenkins/jenkins:lts-jdk17

ARG DOTNET_VERSION=10.0
ARG NODE_MAJOR=22
ARG GITLEAKS_VERSION=8.21.2
ARG TRIVY_VERSION=0.58.1

USER root

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        ca-certificates curl gnupg libicu-dev \
    && install -m 0755 -d /etc/apt/keyrings \
    && curl -fsSL https://download.docker.com/linux/debian/gpg \
        | gpg --dearmor -o /etc/apt/keyrings/docker.gpg \
    && chmod a+r /etc/apt/keyrings/docker.gpg \
    && echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/debian $(. /etc/os-release && echo "$VERSION_CODENAME") stable" \
        > /etc/apt/sources.list.d/docker.list \
    && curl -fsSL https://deb.nodesource.com/setup_${NODE_MAJOR}.x | bash - \
    && apt-get update \
    && apt-get install -y --no-install-recommends docker-ce-cli nodejs \
    && rm -rf /var/lib/apt/lists/*

ENV DOTNET_ROOT=/usr/share/dotnet
ENV PATH=/opt/java/openjdk/bin:/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin:/usr/share/dotnet:/root/.dotnet/tools

RUN curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh \
    && chmod +x /tmp/dotnet-install.sh \
    && /tmp/dotnet-install.sh --channel "${DOTNET_VERSION}" --install-dir "${DOTNET_ROOT}" \
    && rm /tmp/dotnet-install.sh \
    && ln -s "${DOTNET_ROOT}/dotnet" /usr/local/bin/dotnet

# gitleaks — scripts/ci/run-secret-scan.sh (ci/secret-scan stage, git-history secret scanning).
RUN curl -fsSL "https://github.com/gitleaks/gitleaks/releases/download/v${GITLEAKS_VERSION}/gitleaks_${GITLEAKS_VERSION}_linux_x64.tar.gz" \
        -o /tmp/gitleaks.tar.gz \
    && tar -xzf /tmp/gitleaks.tar.gz -C /usr/local/bin gitleaks \
    && rm /tmp/gitleaks.tar.gz \
    && chmod +x /usr/local/bin/gitleaks

# Trivy — scripts/ci/run-image-secret-scan.sh (ci/image-secret-scan stage, container image secret
# scanning; also earmarked by ADR-0012 Action Item 4 for the still-open CVE vulnerability stage).
RUN curl -fsSL "https://github.com/aquasecurity/trivy/releases/download/v${TRIVY_VERSION}/trivy_${TRIVY_VERSION}_Linux-64bit.tar.gz" \
        -o /tmp/trivy.tar.gz \
    && tar -xzf /tmp/trivy.tar.gz -C /usr/local/bin trivy \
    && rm /tmp/trivy.tar.gz \
    && chmod +x /usr/local/bin/trivy

# COREPACK_HOME must live outside /var/jenkins_home: that path is a named Docker volume, mounted
# over whatever the image ships there, so anything baked in under it at build time is invisible at
# runtime. It also must not depend on $HOME resolving the same way at build and run time — this RUN
# executes as root (HOME=/root) but Jenkins actually runs as the jenkins user (HOME=/var/jenkins_home,
# again the volume). Without a fixed COREPACK_HOME, the pin below is written to a cache the running
# container never reads, corepack finds no prepared version at runtime, and it silently fetches
# whatever is latest instead of the pinned 9.15.9 (see frontend/package.json's "packageManager").
ENV COREPACK_HOME=/opt/corepack-cache
RUN corepack enable \
    && corepack prepare pnpm@9.15.9 --activate \
    && chmod -R a+rX "${COREPACK_HOME}"

ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
ENV DOTNET_NOLOGO=1
ENV DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

# Jenkins checks out the workspace as the jenkins user, but root owns most of the tooling above;
# without this, every git command run as a different uid than the directory owner refuses to run.
RUN git config --system --add safe.directory '*'

USER jenkins
