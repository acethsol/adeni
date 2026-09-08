"use client";

import { useEffect } from "react";
import { useSearchParams } from "next/navigation";
import { CustomerMessagesInbox } from "@/components/customer-messages-inbox";

export function CustomerMessagesPageClient() {
  const searchParams = useSearchParams();
  const threadId = searchParams.get("thread");

  useEffect(() => {
    if (!threadId) {
      return;
    }

    const timer = window.setTimeout(() => {
      const event = new CustomEvent("adeni:select-message-thread", { detail: { threadId } });
      window.dispatchEvent(event);
    }, 0);

    return () => window.clearTimeout(timer);
  }, [threadId]);

  return <CustomerMessagesInbox initialThreadId={threadId} />;
}
