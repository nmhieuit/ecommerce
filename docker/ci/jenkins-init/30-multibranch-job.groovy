// Provisions the Multibranch Pipeline job that discovers branches and pull requests on
// nmhieuit/ecommerce and runs the repository-root Jenkinsfile against each
// (specs/012-sonarqube-quality-gate, T014).
//
// As code rather than through New Item, for the same reason as the two scripts beside it: the
// local CI stack is disposable, and a job that only exists as UI state does not survive
// `docker compose -f docker-compose.ci.yml down -v`.
//
// TRIGGERING, and this is a real limitation of the local stack rather than an oversight: GitHub
// cannot reach a Jenkins on localhost:8080, so PR and push events cannot arrive by webhook. This
// job therefore polls on a timer instead. Publishing checks back to GitHub is unaffected, because
// that direction is outbound from Jenkins. A controller with a public address should drop the
// periodic trigger and register a webhook instead — polling is the fallback, not the design.

import com.cloudbees.hudson.plugins.folder.computed.PeriodicFolderTrigger
import jenkins.branch.BranchSource
import jenkins.model.Jenkins
import jenkins.plugins.git.traits.CloneOptionTrait
import hudson.plugins.git.extensions.impl.CloneOption
import org.jenkinsci.plugins.github_branch_source.BranchDiscoveryTrait
import org.jenkinsci.plugins.github_branch_source.GitHubSCMSource
import org.jenkinsci.plugins.github_branch_source.OriginPullRequestDiscoveryTrait
import org.jenkinsci.plugins.workflow.multibranch.WorkflowMultiBranchProject

final String JOB_NAME = 'ecommerce'
final String REPO_OWNER = 'nmhieuit'
final String REPO_NAME = 'ecommerce'
final String CREDENTIAL_ID = 'github-pat'

final Jenkins jenkins = Jenkins.get()

// Idempotent: reconfigure the existing job rather than failing or duplicating it, so a restart
// with changed settings converges instead of erroring.
WorkflowMultiBranchProject job = jenkins.getItemByFullName(JOB_NAME, WorkflowMultiBranchProject)

if (job == null) {
    job = jenkins.createProject(WorkflowMultiBranchProject, JOB_NAME)
    println "[init] Created Multibranch Pipeline job '${JOB_NAME}'."
} else {
    println "[init] Reconfiguring existing Multibranch Pipeline job '${JOB_NAME}'."
}

job.setDisplayName('ecommerce (nmhieuit/ecommerce)')

final GitHubSCMSource source = new GitHubSCMSource(REPO_OWNER, REPO_NAME, null, false)
source.setCredentialsId(CREDENTIAL_ID)

// 1 = discover each branch that is not also filed as a PR; 1 = discover PRs from this origin,
// merged with the target branch, which is what a merge gate should be judging.
source.setTraits([
        new BranchDiscoveryTrait(1),
        new OriginPullRequestDiscoveryTrait(1),
        new CloneOptionTrait(new CloneOption(false, true, null, null)),
])

job.setSourcesList([new BranchSource(source)])

// Polling stands in for the webhook this controller cannot receive — see the note above.
job.addTrigger(new PeriodicFolderTrigger('5m'))

job.save()

println "[init] Job '${JOB_NAME}' -> github.com/${REPO_OWNER}/${REPO_NAME}, credential '${CREDENTIAL_ID}', polling every 5m."
