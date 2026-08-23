// Registers the GitHub token the Multibranch job authenticates with, so it can scan
// nmhieuit/ecommerce and publish the five ci/* checks that branch protection requires
// (specs/012-sonarqube-quality-gate, T014).
//
// Same shape and rationale as 10-sonarqube-server.groovy: the token is read from the gitignored
// .ci-secrets/ directory mounted read-only into the container, never hardcoded, and the whole
// wiring survives `docker compose -f docker-compose.ci.yml down -v`.
//
// GitHub Branch Source expects username/password credentials with the token as the password —
// not secret text — so that is what this creates. The username is only a label to GitHub; any
// value authenticates, but the real login keeps the audit trail readable.
//
// Required token permissions on nmhieuit/ecommerce (fine-grained PAT):
//   Contents        Read-only          — clone and read Jenkinsfile during a scan
//   Metadata        Read-only          — implied, always required
//   Pull requests   Read-only          — PR discovery
//   Commit statuses Read and write     — legacy status reporting
//   Checks          Read and write     — publishChecks(); WITHOUT THIS NO ci/* CHECK APPEARS
//
// A token missing Checks:write authenticates fine and scans fine, then fails at the first
// publishChecks call — so verify the permission, not just the connection.

import com.cloudbees.plugins.credentials.CredentialsScope
import com.cloudbees.plugins.credentials.SystemCredentialsProvider
import com.cloudbees.plugins.credentials.domains.Domain
import com.cloudbees.plugins.credentials.impl.UsernamePasswordCredentialsImpl

final String CREDENTIAL_ID = 'github-pat'
final String GITHUB_LOGIN = 'nmhieuit'
final String TOKEN_FILE = '/run/ci-secrets/github-pat'

final File tokenFile = new File(TOKEN_FILE)

if (!tokenFile.exists()) {
    println "[init] ${TOKEN_FILE} not found — skipping GitHub credential. " +
            'Write the PAT there and restart this container.'
    return
}

final String token = tokenFile.text.trim()

if (token.isEmpty()) {
    println "[init] ${TOKEN_FILE} is empty — skipping GitHub credential."
    return
}

final SystemCredentialsProvider provider = SystemCredentialsProvider.getInstance()
final Domain global = Domain.global()

final UsernamePasswordCredentialsImpl credential = new UsernamePasswordCredentialsImpl(
        CredentialsScope.GLOBAL,
        CREDENTIAL_ID,
        'GitHub PAT for scanning nmhieuit/ecommerce and publishing ci/* checks',
        GITHUB_LOGIN,
        token)

final existing = provider.getCredentials(global).find { it.hasProperty('id') && it.id == CREDENTIAL_ID }

if (existing != null) {
    provider.updateCredentials(global, existing, credential)
    println "[init] Updated credential '${CREDENTIAL_ID}'."
} else {
    provider.addCredentials(global, credential)
    println "[init] Created credential '${CREDENTIAL_ID}'."
}
