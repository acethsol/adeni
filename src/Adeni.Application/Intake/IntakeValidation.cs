namespace Adeni.Application.Intake;

using System.Globalization;
using Adeni.Domain.Common;
using System.Text.Json.Serialization;
using Adeni.Domain.Intake;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record IntakeChoiceDefinition(Guid Id, string Label);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record IntakeFieldDefinition(Guid Id, IntakeFieldType Type, string Label, string? HelpText,
    bool Required, IReadOnlyList<IntakeChoiceDefinition> Choices);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record IntakeDefinition(string Title, string? Description, string? Instructions, IReadOnlyList<IntakeFieldDefinition> Fields);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record IntakeAnswerInput(Guid FieldId, string? Text = null, bool? Boolean = null, string? Date = null, IReadOnlyList<Guid>? Choices = null)
{
    public override string ToString() => $"IntakeAnswerInput {{ FieldId = {FieldId} }}";
}

/// <summary>Bounded non-clinical field validation. Errors never echo definitions or submitted values.</summary>
public static class IntakeValidation
{
    private static Result Invalid() => Result.Failure(new Error("intake.validation"));

    public static Result Definition(IntakeDefinition definition)
    {
        if (definition is null || string.IsNullOrWhiteSpace(definition.Title) || definition.Title.Length > 200
            || definition.Description?.Length > 2000 || definition.Instructions?.Length > 3000
            || definition.Fields is null || definition.Fields.Count is < 1 or > 30 || definition.Fields.Any(x => x is null)
            || definition.Fields.Select(x => x.Id).Distinct().Count() != definition.Fields.Count) return Invalid();
        if (definition.Fields.Any(x => x.Choices?.Any(c => c is null) == true)) return Invalid();
        var optionIds = definition.Fields.SelectMany(x => x.Choices ?? []).Select(x => x.Id).ToArray();
        if (optionIds.Distinct().Count() != optionIds.Length) return Invalid();
        foreach (var field in definition.Fields)
        {
            if (field.Id == Guid.Empty || !Enum.IsDefined(field.Type) || string.IsNullOrWhiteSpace(field.Label)
                || field.Label.Length > 500 || field.HelpText?.Length > 1000 || field.Choices is null) return Invalid();
            var choice = field.Type is IntakeFieldType.SingleChoice or IntakeFieldType.MultipleChoice;
            if ((choice && field.Choices.Count is < 1 or > 20) || (!choice && field.Choices.Count != 0)) return Invalid();
            if (field.Choices.Select(x => x.Id).Distinct().Count() != field.Choices.Count
                || field.Choices.Any(x => x.Id == Guid.Empty || string.IsNullOrWhiteSpace(x.Label) || x.Label.Length > 250)) return Invalid();
        }
        return Result.Success();
    }

    public static Result Answers(IntakeDefinition definition, IReadOnlyList<IntakeAnswerInput> answers)
    {
        if (Definition(definition).IsFailure || answers is null || answers.Count > 30 || answers.Any(x => x is null)
            || answers.Select(x => x.FieldId).Distinct().Count() != answers.Count) return Invalid();
        var fields = definition.Fields.ToDictionary(x => x.Id);
        foreach (var answer in answers)
        {
            if (!fields.TryGetValue(answer.FieldId, out var field)) return Invalid();
            var text = field.Type is IntakeFieldType.ShortText or IntakeFieldType.LongText;
            var boolean = field.Type is IntakeFieldType.YesNo or IntakeFieldType.Acknowledgement;
            var date = field.Type == IntakeFieldType.Date;
            var choice = field.Type is IntakeFieldType.SingleChoice or IntakeFieldType.MultipleChoice;
            if ((!text && answer.Text is not null) || (!boolean && answer.Boolean is not null)
                || (!date && answer.Date is not null) || (!choice && answer.Choices is not null)) return Invalid();
            if (text && (answer.Text is null || answer.Text.Length > (field.Type == IntakeFieldType.ShortText ? 250 : 4000)
                || (field.Required && string.IsNullOrWhiteSpace(answer.Text)))) return Invalid();
            if (boolean && (answer.Boolean is null || (field.Type == IntakeFieldType.Acknowledgement && field.Required && answer.Boolean != true))) return Invalid();
            if (date && !DateOnly.TryParseExact(answer.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) return Invalid();
            if (choice && (answer.Choices is null || answer.Choices.Count > 20
                || (field.Required && answer.Choices.Count == 0)
                || (field.Type == IntakeFieldType.SingleChoice && answer.Choices.Count > 1)
                || answer.Choices.Distinct().Count() != answer.Choices.Count
                || answer.Choices.Any(id => !field.Choices.Any(c => c.Id == id)))) return Invalid();
        }
        if (definition.Fields.Any(f => f.Required && !answers.Any(a => a.FieldId == f.Id))) return Invalid();
        return Result.Success();
    }
}
