import { type AdeniFeedbackService } from "@adeni/ui";

const RETENTION_LOAD_MIN_MS = 320;

/** Global loading panel for retention tab switches + page fetch (pairs with shell `NavigationStart` `showLoading`). */
export async function withRetentionPageLoading<T>(
  feedback: AdeniFeedbackService,
  label: string,
  detail: string,
  work: () => Promise<T>,
): Promise<T> {
  const started = Date.now();
  try {
    return await feedback.runLoading(work, label, detail);
  } finally {
    const remaining = RETENTION_LOAD_MIN_MS - (Date.now() - started);
    if (remaining > 0) {
      await new Promise((resolve) => setTimeout(resolve, remaining));
    }
    feedback.hideLoading();
  }
}
