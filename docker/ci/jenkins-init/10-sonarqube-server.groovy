// Wires the local Jenkins controller to the local SonarQube server, so that
// `withSonarQubeEnv('sonarqube')` and `waitForQualityGate()` in the repository-root Jenkinsfile
// resolve against a real backend (specs/012-sonarqube-quality-gate, T013).
//
// Done as an init script rather than by clicking through Manage Jenkins -> System deliberately:
// the local CI stack is torn down and recreated with `docker compose -f docker-compose.ci.yml
// down -v`, and configuration that only exists as UI state does not survive that. This does, and
// it is reviewable in a pull request.
//
// The token is read from a file mounted into the container, never hardcoded here — see
// docker-compose.ci.yml, which mounts the gitignored .ci-secrets/ directory read-only. Generate
// the token with:
//
//   curl -u admin:<pw> -X POST http://localhost:9000/api/user_tokens/generate \
//        -d name=jenkins-ecommerce -d type=PROJECT_ANALYSIS_TOKEN -d projectKey=ecommerce
//
// Idempotent: re-running replaces the credential and installation rather than duplicating them,
// so a restart with a rotated token picks the new one up.

import com.cloudbees.plugins.credentials.CredentialsScope
import com.cloudbees.plugins.credentials.SystemCredentialsProvider
import com.cloudbees.plugins.credentials.domains.Domain
import hudson.plugins.sonar.SonarGlobalConfiguration
import hudson.plugins.sonar.SonarInstallation
import hudson.plugins.sonar.model.TriggersConfig
import hudson.util.Secret
import jenkins.model.Jenkins
import org.jenkinsci.plugins.plaincredentials.impl.StringCredentialsImpl

// Must match SONARQUBE_SERVER in the Jenkinsfile, or withSonarQubeEnv fails at runtime.
final String INSTALLATION_NAME = 'sonarqube'
final String CREDENTIAL_ID = 'sonarqube-token'
// The compose service name, not localhost: Jenkins reaches SonarQube over the ci-backbone network.
final String SERVER_URL = 'http://sonarqube:9000'
final String TOKEN_FILE = '/run/ci-secrets/sonarqube-analysis-token'

final File tokenFile = new File(TOKEN_FILE)

if (!tokenFile.exists()) {
    println "[init] ${TOKEN_FILE} not found — skipping SonarQube wiring. " +
            'Generate a SonarQube analysis token into .ci-secrets/ and restart this container.'
    return
}

final String token = tokenFile.text.trim()

if (token.isEmpty()) {
    println "[init] ${TOKEN_FILE} is empty — skipping SonarQube wiring."
    return
}

final Jenkins jenkins = Jenkins.get()

// --- Credential -----------------------------------------------------------------------------
final SystemCredentialsProvider credentialsProvider = SystemCredentialsProvider.getInstance()
final Domain global = Domain.global()

final StringCredentialsImpl credential = new StringCredentialsImpl(
        CredentialsScope.GLOBAL,
        CREDENTIAL_ID,
        'SonarQube analysis token for the ecommerce project',
        Secret.fromString(token))

final existing = credentialsProvider.getCredentials(global)
        .find { it.hasProperty('id') && it.id == CREDENTIAL_ID }

if (existing != null) {
    credentialsProvider.updateCredentials(global, existing, credential)
    println "[init] Updated credential '${CREDENTIAL_ID}'."
} else {
    credentialsProvider.addCredentials(global, credential)
    println "[init] Created credential '${CREDENTIAL_ID}'."
}

// --- SonarQube server installation ----------------------------------------------------------
final SonarGlobalConfiguration sonarConfig = jenkins.getDescriptorByType(SonarGlobalConfiguration)

// sonar-plugin 2.18.x exposes two constructors. The seven-argument one takes the token itself as
// its third argument, not a credential id — passing CREDENTIAL_ID there silently stores the
// literal string "sonarqube-token" as the server token, which then fails authentication in a way
// that reads like a bad token rather than a wiring mistake. The nine-argument form is the one that
// takes a credentialsId, so it is the one used here:
//
//   (name, serverUrl, credentialsId, serverAuthenticationToken, mojoVersion,
//    additionalProperties, additionalAnalysisProperties, webhookSecretId, triggers)
final SonarInstallation installation = new SonarInstallation(
        INSTALLATION_NAME,
        SERVER_URL,
        CREDENTIAL_ID,
        null,                    // serverAuthenticationToken — supplied via the credential above
        null,                    // mojoVersion — not a Maven build
        null,                    // additionalProperties — see sonar-project.properties
        null,                    // additionalAnalysisProperties — likewise
        null,                    // webhookSecretId — the local webhook is unauthenticated
        new TriggersConfig())

sonarConfig.setInstallations(
        (sonarConfig.getInstallations().findAll { it.name != INSTALLATION_NAME } + installation)
                as SonarInstallation[])
sonarConfig.save()

println "[init] SonarQube installation '${INSTALLATION_NAME}' -> ${SERVER_URL} configured."
