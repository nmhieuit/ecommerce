#!/usr/bin/env groovy

// CI pipeline for the ecommerce monorepo (spec 012-sonarqube-quality-gate).
//
// Five ordered stages — build, unit tests, integration tests, contract tests, SonarQube quality
// gate — each publishing its own GitHub check under the exact name required by
// specs/012-sonarqube-quality-gate/contracts/pipeline-stage-contract.md §1. Branch protection on
// the protected branch lists those five names as required status checks with bypassing disabled,
// so a failing stage blocks merge for every role (FR-004/FR-005).
//
// Renaming a stage's check name here without updating branch protection silently removes that
// stage from enforcement. Change both in the same PR, and update the contract document.
//
// Agent requirements: a POSIX shell, the .NET 10 SDK, Node 22 with corepack, and a working Docker
// daemon (the integration tier runs Testcontainers — spec 010).

// Publishing statuses explicitly, rather than relying on Jenkins' own stage-name statuses, is what
// pins the names branch protection matches on.
//
// Uses the classic commit-status API (githubNotify, from the `github` plugin) rather than the
// newer Checks API (publishChecks, from `github-checks`): the Checks API can only be authorized by
// a GitHub App installation, never by any personal access token — classic or fine-grained.
// Confirmed directly against this repo's token-permission UI: "Checks" does not appear as a
// grantable permission for a fine-grained PAT at all ("No items available" when searched), so no
// amount of re-scoping a PAT credential could ever have satisfied publishChecks. Commit statuses
// need only "Commit statuses: Read and write", which the existing PAT already has. Classic branch
// protection's required-status-checks list matches on context string regardless of whether it came
// from the Checks API or the status API, so the five names below are unaffected.
void checkStarted(String name) {
    githubNotify context: name, status: 'PENDING', description: "${name} is running", credentialsId: env.GITHUB_CREDENTIALS
}

void checkPassed(String name, String summary) {
    githubNotify context: name, status: 'SUCCESS', description: summary, credentialsId: env.GITHUB_CREDENTIALS
}

void checkFailed(String name, String summary) {
    githubNotify context: name, status: 'FAILURE', description: summary, credentialsId: env.GITHUB_CREDENTIALS
}

pipeline {
    agent any

    options {
        // Bounds the whole run so a hung agent or an unreachable dependency fails the build
        // rather than pinning an executor indefinitely.
        timeout(time: 90, unit: 'MINUTES')
        timestamps()
        disableConcurrentBuilds(abortPrevious: true)
        buildDiscarder(logRotator(numToKeepStr: '50'))
        skipDefaultCheckout(false)
    }

    environment {
        DOTNET_CLI_TELEMETRY_OPTOUT = '1'
        DOTNET_NOLOGO = '1'
        DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'

        SOLUTION = 'Ecommerce.slnx'
        COVERAGE_REPORT = 'artifacts/coverage/backend.cobertura.xml'

        // Name of the SonarQube server installation configured under
        // Manage Jenkins -> System -> SonarQube servers. Must match, or withSonarQubeEnv fails.
        SONARQUBE_SERVER = 'sonarqube'

        // Credential githubNotify authenticates commit-status writes with — see docker/ci/jenkins-init/20-github-credential.groovy.
        GITHUB_CREDENTIALS = 'github-pat'

        // Required status check names — see contracts/pipeline-stage-contract.md §1.
        CHECK_BUILD = 'ci/build'
        CHECK_UNIT = 'ci/unit-tests'
        CHECK_INTEGRATION = 'ci/integration-tests'
        CHECK_CONTRACT = 'ci/contract-tests'
        CHECK_QUALITY_GATE = 'ci/sonarqube-quality-gate'
    }

    stages {

        stage('sonarqube: begin analysis') {
            steps {
                checkStarted(env.CHECK_QUALITY_GATE)
                sh 'dotnet tool restore'
                // `begin` must precede the MSBuild build so the scanner can hook compilation; the
                // matching `end` runs after the contract tier, in the quality gate stage below.
                withSonarQubeEnv(env.SONARQUBE_SERVER) {
                    sh 'scripts/ci/sonar-begin.sh'
                }
            }
            post {
                failure {
                    checkFailed(env.CHECK_QUALITY_GATE,
                        'Could not start SonarQube analysis — the quality gate could not be evaluated.')
                }
            }
        }

        stage('build') {
            steps {
                checkStarted(env.CHECK_BUILD)
                sh 'dotnet restore "$SOLUTION"'
                sh 'dotnet build "$SOLUTION" --configuration Release --no-restore'
                sh 'corepack enable pnpm'
                // `cd` rather than `pnpm --dir frontend ...`: the latter mis-parses when the
                // command itself isn't a pnpm builtin (observed: `pnpm --dir frontend turbo run
                // test` failed with "ERR_PNPM_RECURSIVE_EXEC_FIRST_FAIL Command \"frontend\" not
                // found" in the unit-tests stage below), and running from inside frontend/ is also
                // what lets Corepack resolve frontend/package.json's pinned pnpm version by cwd.
                sh 'cd frontend && pnpm install --frozen-lockfile'
                sh 'cd frontend && pnpm build'
            }
            post {
                success { checkPassed(env.CHECK_BUILD, 'Solution and frontend workspace built.') }
                failure { checkFailed(env.CHECK_BUILD, 'Build failed — see the Jenkins console log.') }
            }
        }

        stage('unit tests') {
            steps {
                checkStarted(env.CHECK_UNIT)
                sh 'scripts/ci/run-dotnet-tests.sh unit'
                sh 'cd frontend && pnpm turbo run test -- --coverage'
                sh 'scripts/ci/merge-coverage.sh'
            }
            post {
                success { checkPassed(env.CHECK_UNIT, 'Backend and frontend unit suites passed.') }
                failure { checkFailed(env.CHECK_UNIT, 'A unit test failed — see the Jenkins console log.') }
            }
        }

        stage('integration tests') {
            steps {
                checkStarted(env.CHECK_INTEGRATION)
                // Testcontainers (spec 010) starts SQL Server, Redis, and RabbitMQ per suite, so
                // this tier needs a reachable Docker daemon on the agent.
                sh 'scripts/ci/run-dotnet-tests.sh integration'
                sh 'scripts/ci/merge-coverage.sh'
            }
            post {
                success { checkPassed(env.CHECK_INTEGRATION, 'Testcontainers integration suites passed.') }
                failure { checkFailed(env.CHECK_INTEGRATION, 'An integration test failed — see the Jenkins console log.') }
            }
        }

        stage('contract tests') {
            steps {
                checkStarted(env.CHECK_CONTRACT)
                sh 'scripts/ci/run-dotnet-tests.sh contract'
                sh 'scripts/ci/merge-coverage.sh'
            }
            post {
                success { checkPassed(env.CHECK_CONTRACT, 'Consumer-driven contract suites passed.') }
                failure { checkFailed(env.CHECK_CONTRACT, 'A contract test failed — see the Jenkins console log.') }
            }
        }

        stage('sonarqube quality gate') {
            steps {
                script {
                    withSonarQubeEnv(env.SONARQUBE_SERVER) {
                        sh 'dotnet sonarscanner end'
                    }

                    // The scanner exits successfully once analysis is *uploaded*; the gate is
                    // computed afterwards on the server. waitForQualityGate blocks on SonarQube's
                    // webhook for the real verdict (research.md Decision 3).
                    //
                    // FR-008 — fail closed: an unreachable or slow SonarQube server must not let a
                    // PR through. The timeout aborts the stage rather than waiting forever, and
                    // any non-OK status is turned into an explicit failure.
                    timeout(time: 15, unit: 'MINUTES') {
                        def qualityGate = waitForQualityGate abortPipeline: false
                        if (qualityGate.status != 'OK') {
                            error("SonarQube quality gate failed with status '${qualityGate.status}'.")
                        }
                        echo 'SonarQube quality gate passed.'
                    }
                }
            }
            post {
                success {
                    checkPassed(env.CHECK_QUALITY_GATE, 'SonarQube quality gate passed.')
                }
                unsuccessful {
                    // Covers FAILURE and the ABORTED result a timeout produces, so a stalled
                    // SonarQube reports a failed check rather than leaving it pending.
                    checkFailed(env.CHECK_QUALITY_GATE,
                        'SonarQube quality gate did not pass — see the analysis on the SonarQube server.')
                }
            }
        }
    }

    post {
        always {
            junit allowEmptyResults: true, testResults: '**/TestResults/*.trx'
            archiveArtifacts artifacts: 'artifacts/coverage/*.xml, frontend/apps/web/coverage/lcov.info',
                             allowEmptyArchive: true,
                             fingerprint: false
        }
    }
}
