"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";

type Props = {
  bookingId: string;
  label?: string;
};

export function WhatsAppBookingButton({ bookingId, label = "Message on WhatsApp" }: Props) {
  const [loading, setLoading] = useState(false);

  async function handleClick() {
    setLoading(true);
    try {
      const response = await fetch(`/api/bookings/${encodeURIComponent(bookingId)}/whatsapp-link`);
      if (!response.ok) {
        return;
      }

      const payload = (await response.json()) as { url: string };
      window.open(payload.url, "_blank", "noopener,noreferrer");
    } finally {
      setLoading(false);
    }
  }

  return (
    <Button type="button" variant="secondary" size="sm" onClick={() => void handleClick()} disabled={loading}>
      {loading ? "Opening…" : label}
    </Button>
  );
}
