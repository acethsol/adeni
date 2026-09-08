"use client";

import { useCallback, useEffect, useState } from "react";
import type { MessageThreadDetail, MessageThreadSummary } from "@adeni/shared";
import { formatWhen } from "@adeni/shared";
import { Button } from "@/components/ui/button";
import { Callout } from "@/components/ui/callout";
import { LoadingPanel } from "@/components/loading-panel";
import { useToast } from "@/contexts/toast-context";
import { useApiErrorMessage } from "@/lib/api-error";
import { cn } from "@/lib/cn";

type Props = {
  initialThreadId?: string | null;
};

export function CustomerMessagesInbox({ initialThreadId = null }: Props) {
  const toast = useToast();
  const { formatApiError } = useApiErrorMessage();
  const [threads, setThreads] = useState<MessageThreadSummary[]>([]);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [detail, setDetail] = useState<MessageThreadDetail | null>(null);
  const [draft, setDraft] = useState("");
  const [loading, setLoading] = useState(true);
  const [loadingDetail, setLoadingDetail] = useState(false);
  const [sending, setSending] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const loadThreads = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const response = await fetch("/api/messages/threads");
      const payload = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(formatApiError(payload, "Could not load messages."));
      }

      const threadsPayload = payload as { items: MessageThreadSummary[] };
      setThreads(threadsPayload.items ?? []);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not load messages.");
    } finally {
      setLoading(false);
    }
  }, [formatApiError]);

  const loadDetail = useCallback(
    async (threadId: string) => {
      setLoadingDetail(true);
      setError(null);
      try {
        const response = await fetch(`/api/messages/threads/${threadId}`);
        const payload = await response.json().catch(() => ({}));
        if (!response.ok) {
          throw new Error(formatApiError(payload, "Could not load conversation."));
        }

        setDetail(payload as MessageThreadDetail);
        await fetch(`/api/messages/threads/${threadId}/read`, { method: "POST" });
        setThreads((current) =>
          current.map((thread) =>
            thread.id === threadId ? { ...thread, unreadCount: 0 } : thread,
          ),
        );
      } catch (err) {
        setError(err instanceof Error ? err.message : "Could not load conversation.");
      } finally {
        setLoadingDetail(false);
      }
    },
    [formatApiError],
  );

  useEffect(() => {
    void loadThreads();
  }, [loadThreads]);

  useEffect(() => {
    if (initialThreadId) {
      setSelectedId(initialThreadId);
    }
  }, [initialThreadId]);

  useEffect(() => {
    function handleSelect(event: Event) {
      const custom = event as CustomEvent<{ threadId: string }>;
      if (custom.detail?.threadId) {
        setSelectedId(custom.detail.threadId);
      }
    }

    window.addEventListener("adeni:select-message-thread", handleSelect);
    return () => window.removeEventListener("adeni:select-message-thread", handleSelect);
  }, []);

  useEffect(() => {
    if (selectedId) {
      void loadDetail(selectedId);
    } else {
      setDetail(null);
    }
  }, [loadDetail, selectedId]);

  async function handleSend(event: React.FormEvent) {
    event.preventDefault();
    if (!selectedId || !draft.trim()) {
      return;
    }

    setSending(true);
    setError(null);
    try {
      const response = await fetch(`/api/messages/threads/${selectedId}/messages`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ body: draft.trim() }),
      });
      const payload = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(formatApiError(payload, "Could not send message."));
      }

      setDraft("");
      toast.success("Message sent.");
      await loadDetail(selectedId);
      await loadThreads();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not send message.");
    } finally {
      setSending(false);
    }
  }

  if (loading) {
    return <LoadingPanel message="Loading messages…" variant="card" />;
  }

  if (error && threads.length === 0) {
    return <Callout tone="error">{error}</Callout>;
  }

  const selected = threads.find((thread) => thread.id === selectedId) ?? null;

  return (
    <div className="grid gap-6 lg:grid-cols-[minmax(240px,320px)_1fr]">
      <aside className="rounded-2xl border border-border bg-surface shadow-sm">
        <div className="border-b border-border px-4 py-3">
          <h2 className="text-sm font-semibold text-foreground">Businesses</h2>
        </div>
        {threads.length === 0 ? (
          <p className="p-4 text-sm text-muted">
            No conversations yet. Message a business from their profile to start chatting.
          </p>
        ) : (
          <ul className="divide-y divide-border">
            {threads.map((thread) => (
              <li key={thread.id}>
                <button
                  type="button"
                  onClick={() => setSelectedId(thread.id)}
                  className={cn(
                    "flex w-full flex-col gap-1 px-4 py-3 text-left transition hover:bg-subtle/60",
                    selectedId === thread.id && "bg-subtle",
                  )}
                >
                  <div className="flex items-center justify-between gap-2">
                    <span className="font-medium text-foreground">
                      {thread.businessName ?? "Business"}
                    </span>
                    {thread.unreadCount > 0 ? (
                      <span className="rounded-full bg-destructive px-2 py-0.5 text-[10px] font-bold text-white">
                        {thread.unreadCount}
                      </span>
                    ) : null}
                  </div>
                  {thread.preview ? (
                    <span className="line-clamp-2 text-xs text-muted">{thread.preview}</span>
                  ) : null}
                  <span className="text-[11px] text-muted-foreground">
                    {formatWhen(thread.lastMessageAt)}
                  </span>
                </button>
              </li>
            ))}
          </ul>
        )}
      </aside>

      <section className="rounded-2xl border border-border bg-surface shadow-sm">
        {!selected ? (
          <p className="p-6 text-sm text-muted">Select a conversation to continue.</p>
        ) : loadingDetail || !detail ? (
          <LoadingPanel message="Loading conversation…" variant="card" className="m-4" />
        ) : (
          <div className="flex min-h-[420px] flex-col">
            <div className="border-b border-border px-4 py-3">
              <h2 className="font-semibold text-foreground">
                {selected.businessName ?? "Business"}
              </h2>
            </div>

            <div className="flex-1 space-y-3 overflow-y-auto p-4">
              {detail.messages.map((message) => (
                <div
                  key={message.id}
                  className={cn(
                    "max-w-[85%] rounded-2xl px-4 py-2 text-sm",
                    message.senderType === "customer"
                      ? "ml-auto bg-primary text-primary-foreground"
                      : "bg-subtle text-foreground",
                  )}
                >
                  <p className="whitespace-pre-wrap">{message.body}</p>
                  <p className="mt-1 text-[10px] opacity-70">{formatWhen(message.createdAt)}</p>
                </div>
              ))}
            </div>

            {error ? <Callout tone="error" className="mx-4 mb-2">{error}</Callout> : null}

            <form className="border-t border-border p-4" onSubmit={(event) => void handleSend(event)}>
              <textarea
                value={draft}
                onChange={(event) => setDraft(event.target.value)}
                rows={3}
                maxLength={4000}
                placeholder="Write a message…"
                className="w-full rounded-xl border border-border px-3 py-2 text-sm"
              />
              <Button type="submit" className="mt-3" disabled={sending || !draft.trim()}>
                {sending ? "Sending…" : "Send message"}
              </Button>
            </form>
          </div>
        )}
      </section>
    </div>
  );
}
