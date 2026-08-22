using PactNet.Infrastructure.Outputters;
using Xunit.Abstractions;

namespace Baskets.Api.ContractTests;

/// <summary>
/// Sends the verifier's own output to the test result rather than to a console nobody reads.
/// </summary>
/// <remarks>
/// Without this a failed verification reports only "Pact verification failed", and the mismatch it
/// found — which interaction, which field, expected against actual — is lost. Naming the field is
/// the whole value of the check: a developer who renamed a response property has to be told that,
/// not merely told that something is wrong (011-consumer-contract-tests FR-005).
/// </remarks>
internal sealed class PactTestOutput(ITestOutputHelper output) : IOutput
{
    public void WriteLine(string line) => output.WriteLine(line);
}
