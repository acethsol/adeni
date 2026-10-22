import type { LocaleId } from "./types";
import { defaultLocale, isLocaleId } from "./types";

const portalCopy: Record<
  LocaleId,
  {
    defaultLanguage: string;
    defaultLanguageHint: string;
    templateOverrides: string;
    templateOverridesHint: string;
    subject: string;
    body: string;
    overridesProOnly: string;
  }
> = {
  en: {
    defaultLanguage: "Default customer notification language",
    defaultLanguageHint:
      "Adeni sends each message in one language using this default when the customer's preference is unknown.",
    templateOverrides: "Message text overrides",
    templateOverridesHint:
      "Optional custom subject and body for key templates. Available on Professional and Business plans.",
    subject: "Subject",
    body: "Body",
    overridesProOnly: "Custom message overrides are on Professional and Business.",
  },
  fr: {
    defaultLanguage: "Langue par défaut des notifications client",
    defaultLanguageHint:
      "Adeni envoie chaque message dans une seule langue, selon cette valeur lorsque la préférence du client est inconnue.",
    templateOverrides: "Personnalisation des messages",
    templateOverridesHint:
      "Objet et corps optionnels pour les modèles clés. Disponible avec les forfaits Professional et Business.",
    subject: "Objet",
    body: "Corps",
    overridesProOnly:
      "La personnalisation des messages est incluse avec Professional et Business.",
  },
};

export function getNotificationSettingsCopy(locale: string) {
  const loc = isLocaleId(locale) ? locale : defaultLocale;
  return portalCopy[loc];
}
