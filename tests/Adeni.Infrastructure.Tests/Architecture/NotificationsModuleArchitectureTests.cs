namespace Adeni.Infrastructure.Tests.Architecture;

using Adeni.Infrastructure.Persistence;
using NetArchTest.Rules;

public sealed class NotificationsModuleArchitectureTests
{
    [Fact]
    public void Notifications_Infrastructure_does_not_reference_Reporting_Infrastructure()
    {
        var infrastructureAssembly = typeof(AdeniDbContext).Assembly;

        var result = Types.InAssembly(infrastructureAssembly)
            .That()
            .ResideInNamespace("Adeni.Infrastructure.Notifications")
            .ShouldNot()
            .HaveDependencyOn("Adeni.Infrastructure.Reporting")
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            $"Notifications must not reference Reporting.Infrastructure. Violations: {string.Join(", ", result.FailingTypes?.Select(t => t.FullName) ?? [])}");
    }
}
