import type { MessageTree } from "../types";

/** Hand-written keys (shared UI chrome, toasts, validation, keys added after the generated catalog). */
export const enExtras: MessageTree = {
  toast: {
    saved: "Saved",
    somethingWrong: "Something went wrong",
    somethingWrongShort: "Something went wrong.",
    notice: "Notice",
    loading: "Loading…",
    loadingDetail: "Just a moment",
    dismiss: "Dismiss",
  },
  portal: {
    payments: {
      couldNotDownloadSalesSummary: "Could not download sales summary export.",
      couldNotDownloadCompensation: "Could not download compensation export.",
    },
    bookings: {
      couldNotDownloadExport: "Could not download the bookings export.",
    },
  },
};
