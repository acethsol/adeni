export type LocaleId = "en" | "fr";

export type LocaleOption = {
  id: LocaleId;
  label: string;
  nativeLabel: string;
};

export const supportedLocales: LocaleOption[] = [
  { id: "en", label: "English", nativeLabel: "English" },
  { id: "fr", label: "French", nativeLabel: "Français" },
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

/** Deep-merge message trees; throws if two trees define the same leaf key. */
export function mergeMessageTrees(...trees: MessageTree[]): MessageTree {
  const out: MessageTree = {};
  for (const tree of trees) {
    mergeInto(out, tree, "");
  }
  return out;
}

function mergeInto(target: MessageTree, source: MessageTree, path: string): void {
  for (const [key, value] of Object.entries(source)) {
    const keyPath = path ? `${path}.${key}` : key;
    const existing = target[key];
    if (isMessageTree(value)) {
      if (existing === undefined) {
        target[key] = {};
      } else if (!isMessageTree(existing)) {
        throw new Error(`i18n key conflict (leaf vs tree): ${keyPath}`);
      }
      mergeInto(target[key] as MessageTree, value, keyPath);
    } else {
      if (existing !== undefined) {
        throw new Error(`i18n duplicate key: ${keyPath}`);
      }
      target[key] = value;
    }
  }
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
