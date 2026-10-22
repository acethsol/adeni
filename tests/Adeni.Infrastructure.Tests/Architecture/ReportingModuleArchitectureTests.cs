namespace Adeni.Infrastructure.Tests.Architecture;

using Adeni.Infrastructure.Persistence;
using NetArchTest.Rules;

public sealed class ReportingModuleArchitectureTests
{
    [Fact]
    public void Reporting_Infrastructure_does_not_reference_Finance_or_Retention_modules()
    {
        var infrastructureAssembly = typeof(AdeniDbContext).Assembly;

        foreach (var forbidden in new[] { "Adeni.Infrastructure.Finance", "Adeni.Infrastructure.Retention" })
        {
            var result = Types.InAssembly(infrastructureAssembly)
                .That()
                .ResideInNamespace("Adeni.Infrastructure.Reporting")
                .ShouldNot()
                .HaveDependencyOn(forbidden)
                .GetResult();

            Assert.True(
                result.IsSuccessful,
                $"Reporting must not reference {forbidden}. Violations: {string.Join(", ", result.FailingTypes?.Select(t => t.FullName) ?? [])}");
        }
    }

    [Fact]
    public void Reporting_Infrastructure_does_not_use_AdeniDbContext()
    {
        var infrastructureAssembly = typeof(AdeniDbContext).Assembly;

        var result = Types.InAssembly(infrastructureAssembly)
            .That()
            .ResideInNamespace("Adeni.Infrastructure.Reporting")
            .ShouldNot()
            .HaveDependencyOn(typeof(AdeniDbContext).FullName!)
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            $"Reporting renderer must not use EF. Violations: {string.Join(", ", result.FailingTypes?.Select(t => t.FullName) ?? [])}");
    }
}
