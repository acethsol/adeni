namespace Adeni.Api.Tests.Architecture;

using Adeni.Api.Controllers;
using Microsoft.AspNetCore.Mvc;

public sealed class ControllerBoundaryTests
{
    [Fact]
    public void Controllers_DoNotInjectEfContexts()
    {
        var controllers = typeof(FinanceController).Assembly.GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type));
        foreach (var controller in controllers)
        {
            Assert.DoesNotContain(controller.GetConstructors().SelectMany(ctor => ctor.GetParameters()),
                parameter => parameter.ParameterType.FullName == "Adeni.Infrastructure.Persistence.AdeniDbContext"
                    || parameter.ParameterType.Namespace?.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) == true);
        }
    }
}
