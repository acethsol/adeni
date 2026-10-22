import type { MessageTree } from "../types";

/** French mirror of `extras-en.ts`. */
export const frExtras: MessageTree = {
  toast: {
    saved: "Enregistré",
    somethingWrong: "Une erreur s'est produite",
    somethingWrongShort: "Une erreur s'est produite.",
    notice: "Avis",
    loading: "Chargement…",
    loadingDetail: "Un instant",
    dismiss: "Fermer",
  },
  portal: {
    payments: {
      couldNotDownloadSalesSummary: "Impossible de télécharger l'exportation du sommaire des ventes.",
      couldNotDownloadCompensation: "Impossible de télécharger l'exportation des commissions.",
    },
    bookings: {
      couldNotDownloadExport: "Impossible de télécharger l'exportation des réservations.",
    },
  },
};
