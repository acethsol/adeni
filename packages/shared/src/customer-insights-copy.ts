import type { CustomerSegmentId } from "./customer-segments";
const copy = {
  en: {
    title: "Customer segments", preview: "Preview", close: "Close", open: "Open PDF", download: "Download PDF", loading: "Loading…", error: "This view is unavailable. Try again or use the existing download action.",
    available: "No current matches", notAvailable: "Source unavailable", partial: "Incomplete source history", notEntitled: "Customer analytics is unavailable on the current plan", erased: "Unavailable following privacy erasure",
    scope: "Location scope", business: "Business scope", approximate: "Some visit dates use legacy queue arrival dates.", evaluated: "Evaluated", draft: "Draft — this preview does not issue or send the document.",
    newCustomer: "Newly served", recentlyActive: "Recently active", lapsed: "Lapsed", atRisk: "At risk", vip: "VIP — frequent visitor", frequentQueueNoShow: "Repeated queue no-shows",
    whatAreSegments: "What are segments?",
    matchDetails: "Why these labels?",
    definitions: "Platform defaults v1: newly served means one lifetime recorded visit within 30 dates; recent activity means a visit within 30 dates; lapsed means 90 dates without a visit; at risk means 3 visits within 180 dates and last visit 30–89 dates ago; VIP means 6 visits within 180 dates and a visit within 30 dates. Lapsed/at risk require no upcoming booking. Repeated queue no-shows means 2 explicit records within 90 dates. These labels do not measure spend or appointment no-shows.",
  },
  fr: {
    title: "Segments clients", preview: "Aperçu", close: "Fermer", open: "Ouvrir le PDF", download: "Télécharger le PDF", loading: "Chargement…", error: "Cette vue est indisponible. Réessayez ou utilisez le téléchargement existant.",
    available: "Aucun segment actuel", notAvailable: "Source indisponible", partial: "Historique source incomplet", notEntitled: "Les analyses clients ne sont pas disponibles avec le forfait actuel", erased: "Indisponible après effacement des données",
    scope: "Portée : établissement", business: "Portée : entreprise", approximate: "Certaines dates utilisent l’arrivée en file d’attente historique.", evaluated: "Évalué", draft: "Brouillon — cet aperçu n’émet ni n’envoie le document.",
    newCustomer: "Nouvellement servi", recentlyActive: "Récemment actif", lapsed: "Inactif", atRisk: "À risque", vip: "VIP — visites fréquentes", frequentQueueNoShow: "Absences répétées en file d’attente",
    whatAreSegments: "Qu’est-ce qu’un segment ?",
    matchDetails: "Pourquoi ces libellés ?",
    definitions: "Valeurs par défaut v1 : nouvellement servi = une seule visite enregistrée, dans les 30 derniers jours calendaires; actif = une visite dans les 30 jours; inactif = aucune visite depuis 90 jours; à risque = 3 visites dans les 180 jours, dernière visite il y a 30 à 89 jours; VIP = 6 visites dans les 180 jours et une dans les 30 jours. Inactif/à risque exigent l’absence de réservation à venir. Absences répétées en file = 2 absences explicites dans les 90 jours. Ces segments ne mesurent ni les dépenses ni les absences aux rendez-vous.",
  },
};
export function customerInsightsCopy(locale: string, key: keyof typeof copy.en): string { return copy[locale === "fr" ? "fr" : "en"][key]; }
export function customerSegmentReason(locale: string, id: CustomerSegmentId, count: number, days: number | null): string {
  if (locale === "fr") return id === "frequentQueueNoShow" ? `${count} absences enregistrées en file d’attente sur 90 jours.` : `${count} visite(s) enregistrée(s); dernière visite il y a ${days} jour(s).`;
  return id === "frequentQueueNoShow" ? `${count} recorded queue no-shows in 90 dates.` : `${count} recorded visit(s); last visit ${days} date(s) ago.`;
}
