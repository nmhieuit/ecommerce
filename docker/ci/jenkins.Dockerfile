# Jenkins controller image with the toolchain the Jenkinsfile's agent requires: a POSIX shell,
# the .NET 10 SDK, Node 22 with corepack/pnpm, the Docker CLI (talking to the host daemon via the
# mounted socket — see docker-compose.ci.yml), and git.
#
# This reconstructs an image that previously existed only as a manually-built, untagged local
# image (`ecomerce-ci-jenkins:local`) — reproducible nowhere else and silently lost the moment the
# container was recreated from docker-compose.ci.yml's plain `jenkins/jenkins:lts-jdk17`. Captured
# from that image's actual build history (`docker history ecomerce-ci-jenkins:local --no-trunc`)
# after diagnosing a `dotnet: not found` failure caused by exactly that loss.
FROM jenkins/jenkins:lts-jdk17

ARG DOTNET_VERSION=10.0
ARG NODE_MAJOR=22

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

RUN corepack enable \
    && corepack prepare pnpm@9.15.9 --activate

ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
ENV DOTNET_NOLOGO=1
ENV DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

# Jenkins checks out the workspace as the jenkins user, but root owns most of the tooling above;
# without this, every git command run as a different uid than the directory owner refuses to run.
RUN git config --system --add safe.directory '*'

USER jenkins
