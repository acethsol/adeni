export type LocaleId = "en" | "fr" | "es" | "pt";

export type LocaleOption = {
  id: LocaleId;
  label: string;
  nativeLabel: string;
};

export const supportedLocales: LocaleOption[] = [
  { id: "en", label: "English", nativeLabel: "English" },
  { id: "fr", label: "French", nativeLabel: "Français" },
  { id: "es", label: "Spanish", nativeLabel: "Español" },
  { id: "pt", label: "Portuguese", nativeLabel: "Português" },
];

export const defaultLocale: LocaleId = "en";

export const LOCALE_COOKIE_NAME = "adeni_locale";

export type MessageTree = {
  [key: string]: string | MessageTree;
};

export type Messages = MessageTree;

function isMessageTree(value: string | MessageTree): value is MessageTree {
  return typeof value === "object";
}

export function translate(
  messages: Messages,
  key: string,
  vars?: Record<string, string | number>,
): string {
  const parts = key.split(".");
  let current: string | MessageTree = messages;

  for (const part of parts) {
    if (!isMessageTree(current) || !(part in current)) {
      return key;
    }

    current = current[part];
  }

  if (typeof current !== "string") {
    return key;
  }

  if (!vars) {
    return current;
  }

  return Object.entries(vars).reduce(
    (result, [name, value]) => result.replaceAll(`{${name}}`, String(value)),
    current,
  );
}

export function getLocaleOption(locale: string): LocaleOption {
  return supportedLocales.find((entry) => entry.id === locale) ?? supportedLocales[0];
}

export function isLocaleId(value: string): value is LocaleId {
  return supportedLocales.some((entry) => entry.id === value);
}
