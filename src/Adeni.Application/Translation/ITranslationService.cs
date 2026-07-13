namespace Adeni.Application.Translation;

using Adeni.Domain.Common;

public sealed class TranslationOptions
{
    public const string SectionName = "Translation";

    public string Endpoint { get; set; } = "https://api.cognitive.microsofttranslator.com";

    public string? Key { get; set; }

    public string? Region { get; set; }

    public bool Enabled => !string.IsNullOrWhiteSpace(Key);
}

public sealed record TranslationBatchResult(IReadOnlyDictionary<string, string> Translations);

public interface ITranslationService
{
    Task<Result<TranslationBatchResult>> TranslateAsync(
        IReadOnlyList<string> texts,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken = default);
}
