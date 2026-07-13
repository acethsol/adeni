import type { LocaleId } from "@adeni/shared";

function getApiBaseUrl() {
  return (
    process.env.ADENI_API_URL?.trim() ||
    process.env.NEXT_PUBLIC_ADENI_API_URL?.trim() ||
    "http://localhost:5169"
  );
}

export async function translateTextMap(
  texts: string[],
  source: LocaleId,
  target: LocaleId,
): Promise<Map<string, string>> {
  const unique = [...new Set(texts.map((text) => text.trim()).filter(Boolean))];

  if (unique.length === 0) {
    return new Map();
  }

  if (source === target) {
    return new Map(unique.map((text) => [text, text]));
  }

  const response = await fetch(`${getApiBaseUrl()}/api/v1/translate`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      texts: unique,
      source,
      target,
    }),
    cache: "no-store",
  });

  if (!response.ok) {
    throw new Error("Translation request failed");
  }

  const payload = (await response.json()) as { translations?: Record<string, string> };
  const translations = payload.translations ?? {};

  return new Map(unique.map((text) => [text, translations[text] ?? text]));
}
