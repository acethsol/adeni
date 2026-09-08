"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { Button } from "@/components/ui/button";
import { Callout } from "@/components/ui/callout";
import { useApiErrorMessage } from "@/lib/api-error";

type Props = {
  tenantId: string;
  businessName: string;
  bookingId?: string;
  loginHref: string;
  isAuthenticated: boolean;
};

export function MessageBusinessButton({
  tenantId,
  businessName,
  bookingId,
  loginHref,
  isAuthenticated,
}: Props) {
  const router = useRouter();
  const { formatApiError } = useApiErrorMessage();
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (!isAuthenticated) {
    return (
      <Button href={loginHref} variant="secondary">
        Message {businessName} on Adeni
      </Button>
    );
  }

  async function handleStartConversation() {
    setLoading(true);
    setError(null);

    try {
      const response = await fetch("/api/messages/threads", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          tenantId,
          ...(bookingId ? { bookingId } : {}),
        }),
      });

      const payload = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(formatApiError(payload, "Could not start conversation."));
      }

      router.push(`/my-messages?thread=${payload.id}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not start conversation.");
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="space-y-2">
      <Button type="button" variant="secondary" onClick={() => void handleStartConversation()} disabled={loading}>
        {loading ? "Opening chat…" : `Message ${businessName} on Adeni`}
      </Button>
      {error ? <Callout tone="error">{error}</Callout> : null}
    </div>
  );
}
