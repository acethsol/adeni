"use client";

import { useCallback, useState } from "react";
import QRCode from "react-qr-code";
import { Copy, MessageCircle, Share2, Sparkles } from "lucide-react";
import { BusinessPortalCard } from "@/components/business-portal-card";
import { Button } from "@/components/ui/button";
import { useToast } from "@/contexts/toast-context";

type Props = {
  businessName: string;
  slug: string;
  phone?: string;
};

function buildPublicUrl(slug: string) {
  if (typeof window !== "undefined") {
    return `${window.location.origin}/businesses/${slug}`;
  }
  return `/businesses/${slug}`;
}

export function BusinessShareKit({ businessName, slug, phone }: Props) {
  const toast = useToast();
  const [publicUrl, setPublicUrl] = useState(() => buildPublicUrl(slug));

  const refreshUrl = useCallback(() => {
    setPublicUrl(buildPublicUrl(slug));
  }, [slug]);

  const copyLink = async () => {
    refreshUrl();
    try {
      await navigator.clipboard.writeText(publicUrl);
      toast.success("Booking link copied");
    } catch {
      toast.error("Could not copy link");
    }
  };

  const whatsAppMessage = encodeURIComponent(
    `Book ${businessName} on Adeni — pick a service and time:\n${publicUrl}`,
  );
  const whatsAppHref = phone
    ? `https://wa.me/?text=${whatsAppMessage}`
    : `https://wa.me/?text=${whatsAppMessage}`;

  const instagramBio = `${businessName} — book online:\n${publicUrl}\n\nAdd this link to your Instagram bio or story.`;

  const copyInstagramTemplate = async () => {
    try {
      await navigator.clipboard.writeText(instagramBio);
      toast.success("Instagram bio template copied");
    } catch {
      toast.error("Could not copy template");
    }
  };

  return (
    <BusinessPortalCard padding="lg">
      <div className="flex items-center gap-2">
        <Share2 className="h-5 w-5 text-accent" aria-hidden />
        <h2 className="text-lg font-semibold text-foreground">Share kit</h2>
      </div>
      <p className="mt-1 text-sm text-muted">
        Copy your public booking link or share templates for WhatsApp and Instagram.
      </p>

      <div className="mt-6 grid gap-6 lg:grid-cols-[auto_1fr] lg:items-start">
        <div className="mx-auto rounded-xl border border-border bg-white p-4 shadow-sm">
          <QRCode value={publicUrl} size={128} aria-label="QR code for public booking page" />
        </div>

        <div className="space-y-4">
          <div>
            <p className="text-xs font-bold uppercase tracking-wider text-accent">Public link</p>
            <p className="mt-2 break-all rounded-lg bg-subtle px-3 py-2 text-sm text-foreground">{publicUrl}</p>
            <Button type="button" variant="secondary" size="sm" className="mt-2" onClick={() => void copyLink()}>
              <Copy className="mr-2 h-4 w-4" aria-hidden />
              Copy link
            </Button>
          </div>

          <div className="flex flex-wrap gap-2">
            <Button href={whatsAppHref} target="_blank" rel="noopener noreferrer" variant="secondary" size="sm">
              <MessageCircle className="mr-2 h-4 w-4" aria-hidden />
              WhatsApp template
            </Button>
            <Button type="button" variant="secondary" size="sm" onClick={() => void copyInstagramTemplate()}>
              <Sparkles className="mr-2 h-4 w-4" aria-hidden />
              Instagram bio
            </Button>
          </div>
        </div>
      </div>
    </BusinessPortalCard>
  );
}
