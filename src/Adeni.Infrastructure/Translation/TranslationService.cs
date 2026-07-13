namespace Adeni.Infrastructure.Translation;

using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Adeni.Application.Caching;
using Adeni.Application.Translation;
using Adeni.Domain.Common;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public sealed class TranslationService(
    ICacheService cache,
    IHttpClientFactory httpClientFactory,
    IOptions<TranslationOptions> options,
    IHostEnvironment environment,
    ILogger<TranslationService> logger) : ITranslationService
{
    private const int MaxTextsPerRequest = 50;
    private const int MaxCharsPerRequest = 10_000;
    private const int AzureBatchSize = 100;
    private static readonly HashSet<string> SupportedLanguages = new(StringComparer.OrdinalIgnoreCase)
    {
        "en",
        "fr",
        "es",
        "pt",
    };

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task<Result<TranslationBatchResult>> TranslateAsync(
        IReadOnlyList<string> texts,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken = default)
    {
        var source = NormalizeLanguage(sourceLanguage);
        var target = NormalizeLanguage(targetLanguage);

        if (source is null || target is null)
        {
            return Result.Failure<TranslationBatchResult>(
                Error.Validation("Unsupported source or target language."));
        }

        if (texts.Count == 0)
        {
            return Result.Success(new TranslationBatchResult(new Dictionary<string, string>()));
        }

        if (texts.Count > MaxTextsPerRequest)
        {
            return Result.Failure<TranslationBatchResult>(
                Error.Validation($"At most {MaxTextsPerRequest} texts are allowed per request."));
        }

        var normalized = texts
            .Select(text => text.Trim())
            .Where(text => text.Length > 0)
            .ToArray();

        if (normalized.Sum(text => text.Length) > MaxCharsPerRequest)
        {
            return Result.Failure<TranslationBatchResult>(
                Error.Validation($"At most {MaxCharsPerRequest} characters are allowed per request."));
        }

        if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Success(new TranslationBatchResult(normalized.ToDictionary(text => text, text => text)));
        }

        var unique = normalized.Distinct(StringComparer.Ordinal).ToArray();
        var translatedByOriginal = new Dictionary<string, string>(StringComparer.Ordinal);

        var pending = new List<string>();
        foreach (var text in unique)
        {
            var cached = await cache.GetAsync<TranslationCacheEntry>(
                CacheKeys.Translation(source, target, HashText(text)),
                cancellationToken);

            if (cached?.Text is { Length: > 0 })
            {
                translatedByOriginal[text] = cached.Text;
            }
            else
            {
                pending.Add(text);
            }
        }

        if (pending.Count > 0)
        {
            var providerResult = await TranslateWithProviderAsync(
                pending,
                source,
                target,
                cancellationToken);

            if (providerResult.IsFailure)
            {
                return Result.Failure<TranslationBatchResult>(providerResult.Error);
            }

            foreach (var (original, translated) in providerResult.Value!)
            {
                translatedByOriginal[original] = translated;
                await cache.SetAsync(
                    CacheKeys.Translation(source, target, HashText(original)),
                    new TranslationCacheEntry(translated),
                    CacheTtl.Translation,
                    cancellationToken);
            }
        }

        var response = normalized.ToDictionary(
            text => text,
            text => translatedByOriginal.TryGetValue(text, out var translated) ? translated : text,
            StringComparer.Ordinal);

        return Result.Success(new TranslationBatchResult(response));
    }

    private async Task<Result<IReadOnlyDictionary<string, string>>> TranslateWithProviderAsync(
        IReadOnlyList<string> texts,
        string source,
        string target,
        CancellationToken cancellationToken)
    {
        var translationOptions = options.Value;
        if (translationOptions.Enabled)
        {
            return await TranslateWithAzureAsync(texts, source, target, translationOptions, cancellationToken);
        }

        if (environment.IsDevelopment() || environment.EnvironmentName == "Testing")
        {
            return await TranslateWithDevFallbackAsync(texts, source, target, cancellationToken);
        }

        logger.LogError("Translation provider is not configured for {Environment}", environment.EnvironmentName);
        return Result.Failure<IReadOnlyDictionary<string, string>>(
            new Error("translation_unavailable", "Translation is not configured."));
    }

    private async Task<Result<IReadOnlyDictionary<string, string>>> TranslateWithAzureAsync(
        IReadOnlyList<string> texts,
        string source,
        string target,
        TranslationOptions translationOptions,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("azure-translator");
        var endpoint = translationOptions.Endpoint.TrimEnd('/');
        var results = new Dictionary<string, string>(StringComparer.Ordinal);

        for (var offset = 0; offset < texts.Count; offset += AzureBatchSize)
        {
            var batch = texts.Skip(offset).Take(AzureBatchSize).ToArray();
            var requestUri =
                $"{endpoint}/translate?api-version=3.0&from={Uri.EscapeDataString(source)}&to={Uri.EscapeDataString(target)}";
            using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
            request.Headers.Add("Ocp-Apim-Subscription-Key", translationOptions.Key);

            if (!string.IsNullOrWhiteSpace(translationOptions.Region))
            {
                request.Headers.Add("Ocp-Apim-Subscription-Region", translationOptions.Region);
            }

            request.Content = JsonContent.Create(
                batch.Select(text => new AzureTranslateInput(text)).ToArray(),
                options: SerializerOptions);

            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogWarning(
                    "Azure Translator failed with status {StatusCode}: {Body}",
                    (int)response.StatusCode,
                    body);
                return Result.Failure<IReadOnlyDictionary<string, string>>(
                    new Error("translation_failed", "Translation provider request failed."));
            }

            var payload = await response.Content.ReadFromJsonAsync<AzureTranslateOutput[]>(
                SerializerOptions,
                cancellationToken);

            if (payload is null || payload.Length != batch.Length)
            {
                return Result.Failure<IReadOnlyDictionary<string, string>>(
                    new Error("translation_failed", "Translation provider returned an invalid payload."));
            }

            for (var index = 0; index < batch.Length; index++)
            {
                var translated = payload[index].Translations?.FirstOrDefault()?.Text?.Trim();
                results[batch[index]] = string.IsNullOrWhiteSpace(translated) ? batch[index] : translated;
            }
        }

        return Result.Success<IReadOnlyDictionary<string, string>>(results);
    }

    private async Task<Result<IReadOnlyDictionary<string, string>>> TranslateWithDevFallbackAsync(
        IReadOnlyList<string> texts,
        string source,
        string target,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("dev-translator");
        var results = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var text in texts)
        {
            var url = new Uri(
                $"https://api.mymemory.translated.net/get?q={Uri.EscapeDataString(text)}&langpair={Uri.EscapeDataString(source)}|{Uri.EscapeDataString(target)}");

            try
            {
                using var response = await client.GetAsync(url, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    results[text] = text;
                    continue;
                }

                var payload = await response.Content.ReadFromJsonAsync<MyMemoryResponse>(
                    SerializerOptions,
                    cancellationToken);
                var translated = payload?.ResponseData?.TranslatedText?.Trim();
                results[text] = string.IsNullOrWhiteSpace(translated) ? text : translated;
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Dev translation fallback failed for text hash {Hash}", HashText(text));
                results[text] = text;
            }
        }

        return Result.Success<IReadOnlyDictionary<string, string>>(results);
    }

    private static string? NormalizeLanguage(string language)
    {
        var normalized = language.Trim().ToLowerInvariant();
        return SupportedLanguages.Contains(normalized) ? normalized : null;
    }

    private static string HashText(string text)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private sealed record TranslationCacheEntry(string Text);

    private sealed record AzureTranslateInput(string Text);

    private sealed record AzureTranslateOutput(AzureTranslation[]? Translations);

    private sealed record AzureTranslation(string Text);

    private sealed record MyMemoryResponse(MyMemoryData? ResponseData);

    private sealed record MyMemoryData(string? TranslatedText);
}
