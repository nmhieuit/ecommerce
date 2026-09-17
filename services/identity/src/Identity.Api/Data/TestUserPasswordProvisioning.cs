using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;

namespace Identity.Api.Data;

/// <summary>
/// Sets the password on the row <see cref="TestUserSeed"/> already created via <c>HasData</c> —
/// invoked via the <c>--set-test-user-password</c> flag (<c>Program.cs</c>), folded into
/// <c>identity-migrate</c>'s entrypoint so it runs on every stack that already runs the schema
/// migration, with no separate container or manual step.
/// </summary>
/// <remarks>
/// Optional, not a <c>RequiredSecret</c>: unlike <c>ConnectionStrings:IdentityDb</c>, there is no
/// environment where a missing password should take the whole migrate step down — a stack with no
/// <c>TestUserPassword</c> configured simply ends up with a test account nobody can log into yet,
/// which is a safe default (constitution Principle VI: no credential is invented on this class's
/// behalf; the value must come from the caller's environment, real or local).
/// </remarks>
public static class TestUserPasswordProvisioning
{
    public static async Task SetPasswordIfConfiguredAsync(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        await using var scope = services.CreateAsyncScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var password = configuration["TestUserPassword"];

        if (string.IsNullOrWhiteSpace(password))
        {
            Console.WriteLine("TestUserPassword is not configured — skipping test user password provisioning.");
            return;
        }

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByIdAsync(TestUserSeed.Id);
        if (user is null)
        {
            // TestUserSeed.User is HasData on the same DbContext this container just migrated —
            // reaching here means the migration didn't run, not that the password is optional.
            await Console.Error.WriteLineAsync($"Test user '{TestUserSeed.Email}' (id {TestUserSeed.Id}) not found — migration did not seed it.");
            Environment.ExitCode = 1;
            return;
        }

        // Re-set rather than skip-if-present: keeps the password in sync with whatever is
        // currently configured on every run, the same "rotate without redeploy" expectation
        // specs/018-cluster-secret-store/contracts/external-secret-manifest-contract.md already
        // states for other secrets (5-minute refreshInterval), rather than "first write wins".
        if (await userManager.HasPasswordAsync(user))
        {
            var removeResult = await userManager.RemovePasswordAsync(user);
            if (!removeResult.Succeeded)
            {
                await Console.Error.WriteLineAsync(
                    $"Failed to clear existing password for '{TestUserSeed.Email}': {string.Join("; ", removeResult.Errors.Select(e => e.Description))}");
                Environment.ExitCode = 1;
                return;
            }
        }

        var addResult = await userManager.AddPasswordAsync(user, password);
        if (!addResult.Succeeded)
        {
            await Console.Error.WriteLineAsync(
                $"Failed to set password for '{TestUserSeed.Email}': {string.Join("; ", addResult.Errors.Select(e => e.Description))}");
            Environment.ExitCode = 1;
            return;
        }

        Console.WriteLine($"Password set for test user '{TestUserSeed.Email}'.");
    }
}
