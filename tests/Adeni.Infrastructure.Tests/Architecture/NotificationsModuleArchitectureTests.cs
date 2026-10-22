namespace Adeni.Infrastructure.Tests.Architecture;

using Adeni.Infrastructure.Persistence;
using NetArchTest.Rules;

public sealed class NotificationsModuleArchitectureTests
{
    [Fact]
    public void Notification_event_handlers_do_not_reference_Resend_or_Twilio_providers()
    {
        var infrastructureAssembly = typeof(AdeniDbContext).Assembly;

        var result = Types.InAssembly(infrastructureAssembly)
            .That()
            .ImplementInterface(typeof(Adeni.Application.Events.IDomainEventHandler<>))
            .And()
            .ResideInNamespace("Adeni.Infrastructure.Notifications")
            .ShouldNot()
            .HaveDependencyOnAny(
                "Adeni.Infrastructure.Notifications.ResendEmailNotificationProvider",
                "Adeni.Infrastructure.Notifications.TwilioSmsNotificationProvider")
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            $"Notification handlers must schedule jobs, not call providers directly. Violations: {string.Join(", ", result.FailingTypes?.Select(t => t.FullName) ?? [])}");
    }

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
