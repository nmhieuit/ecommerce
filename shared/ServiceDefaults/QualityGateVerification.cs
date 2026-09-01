namespace ServiceDefaults;

// TEMP (specs/013-sonarqube-merge-blocker T018): deliberately introduces a real SonarQube
// violation (an empty catch block, uncovered by any test) so a real PR fails the actual quality
// gate — not just a broken unit test — to verify Scenario 2/3/4 end to end. Removed in the very
// next commit on this branch once the failing/blocked/decorated state has been observed on GitHub.
public static class QualityGateVerification
{
    public static void SwallowFailures(Action action)
    {
        try
        {
            action();
        }
        catch (Exception)
        {
        }
    }
}
