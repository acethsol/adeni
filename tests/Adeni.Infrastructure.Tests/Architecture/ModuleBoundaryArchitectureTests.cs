namespace Adeni.Infrastructure.Tests.Architecture;

using Adeni.Infrastructure.Persistence;
using NetArchTest.Rules;

public sealed class ModuleBoundaryArchitectureTests
{
    private static readonly string[] ModuleNamespaces =
    [
        "Adeni.Infrastructure.Booking",
        "Adeni.Infrastructure.Tenancy",
        "Adeni.Infrastructure.Discovery",
        "Adeni.Infrastructure.Reviews",
        "Adeni.Infrastructure.Admin",
        "Adeni.Infrastructure.Identity",
        "Adeni.Infrastructure.Catalog",
        "Adeni.Infrastructure.Markets",
        "Adeni.Infrastructure.Storage",
        "Adeni.Infrastructure.Notifications",
        "Adeni.Infrastructure.Payments",
        "Adeni.Infrastructure.Subscriptions",
        "Adeni.Infrastructure.Messaging",
    ];

    [Fact]
    public void Infrastructure_modules_do_not_reference_each_other_directly()
    {
        var infrastructureAssembly = typeof(AdeniDbContext).Assembly;

        foreach (var sourceModule in ModuleNamespaces)
        {
            foreach (var targetModule in ModuleNamespaces)
            {
                if (string.Equals(sourceModule, targetModule, StringComparison.Ordinal))
                {
                    continue;
                }

                var result = Types.InAssembly(infrastructureAssembly)
                    .That()
                    .ResideInNamespace(sourceModule)
                    .ShouldNot()
                    .HaveDependencyOn(targetModule)
                    .GetResult();

                Assert.True(
                    result.IsSuccessful,
                    BuildFailureMessage(sourceModule, targetModule, result.FailingTypes));
            }
        }
    }

    private static string BuildFailureMessage(
        string sourceModule,
        string targetModule,
        IEnumerable<Type>? failingTypes) =>
        $"{sourceModule} must not reference {targetModule}. Violations: {string.Join(", ", failingTypes?.Select(t => t.FullName) ?? [])}";
}
