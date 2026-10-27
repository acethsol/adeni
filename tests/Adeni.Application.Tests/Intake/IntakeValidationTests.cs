namespace Adeni.Application.Tests.Intake;

using Adeni.Application.Intake;
using Adeni.Domain.Intake;

public sealed class IntakeValidationTests
{
    private static readonly Guid Field = Guid.NewGuid();
    private static IntakeDefinition Definition(IntakeFieldType type, bool required = true) =>
        new("Consultation", null, null, [new(Field, type, "Preference", null, required, [])]);

    [Theory]
    [InlineData(IntakeFieldType.YesNo, true)]
    [InlineData(IntakeFieldType.Acknowledgement, false)]
    public void RequiredBoolean_FalseHasTypeSpecificMeaning(IntakeFieldType type, bool success) =>
        Assert.Equal(success, IntakeValidation.Answers(Definition(type), [new(Field, Boolean: false)]).IsSuccess);

    [Theory]
    [InlineData("2024-02-29", true)]
    [InlineData("2025-02-29", false)]
    [InlineData("29/02/2024", false)]
    [InlineData("2024-02-29T12:00:00Z", false)]
    public void Date_RequiresRealIsoCalendarDate(string value, bool success) =>
        Assert.Equal(success, IntakeValidation.Answers(Definition(IntakeFieldType.Date), [new(Field, Date: value)]).IsSuccess);

    [Fact]
    public void Answers_RejectMissingDuplicateUnknownAndMixedTypes()
    {
        var definition = Definition(IntakeFieldType.ShortText);
        Assert.True(IntakeValidation.Answers(definition, []).IsFailure);
        Assert.True(IntakeValidation.Answers(definition, [new(Field, Text: " ")]).IsFailure);
        Assert.True(IntakeValidation.Answers(definition, [new(Field, Text: "a"), new(Field, Text: "b")]).IsFailure);
        Assert.True(IntakeValidation.Answers(definition, [new(Guid.NewGuid(), Text: "a")]).IsFailure);
        Assert.True(IntakeValidation.Answers(definition, [new(Field, Text: "a", Boolean: true)]).IsFailure);
        Assert.True(IntakeValidation.Answers(Definition(IntakeFieldType.ShortText, false), []).IsSuccess);
    }

    [Fact]
    public void Choices_AreBoundToFieldAndDoNotAcceptDuplicates()
    {
        var option = Guid.NewGuid();
        var definition = new IntakeDefinition("Consultation", null, null,
            [new(Field, IntakeFieldType.MultipleChoice, "Preferences", null, true, [new(option, "Quiet appointment")])]);
        Assert.True(IntakeValidation.Answers(definition, [new(Field, Choices: [option])]).IsSuccess);
        Assert.True(IntakeValidation.Answers(definition, [new(Field, Choices: [option, option])]).IsFailure);
        Assert.True(IntakeValidation.Answers(definition, [new(Field, Choices: [Guid.NewGuid()])]).IsFailure);
        Assert.True(IntakeValidation.Answers(definition, [new(Field, Choices: [])]).IsFailure);
    }

    [Fact]
    public void ValidationErrors_DoNotEchoSensitiveContent()
    {
        const string sentinel = "sensitive-answer-sentinel";
        var result = IntakeValidation.Answers(Definition(IntakeFieldType.YesNo), [new(Field, Text: sentinel)]);
        Assert.True(result.IsFailure);
        Assert.DoesNotContain(sentinel, System.Text.Json.JsonSerializer.Serialize(result.Error));
    }

    [Fact]
    public void DefinitionAndText_EnforceTechnicalBounds()
    {
        Assert.True(IntakeValidation.Definition(Definition((IntakeFieldType)999)).IsFailure);
        Assert.True(IntakeValidation.Answers(Definition(IntakeFieldType.ShortText), [new(Field, Text: new string('x', 251))]).IsFailure);
        Assert.True(IntakeValidation.Answers(Definition(IntakeFieldType.LongText), [new(Field, Text: "Unicode: Àda 🌿\nsecond line")]).IsSuccess);
    }
}
