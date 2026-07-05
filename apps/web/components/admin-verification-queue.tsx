"use client";

import { useCallback, useEffect, useState } from "react";
import type { PendingBusiness } from "@adeni/shared";

type Props = {
  initialItems: PendingBusiness[];
  initialError: string | null;
};

export function AdminVerificationQueue({ initialItems, initialError }: Props) {
  const [items, setItems] = useState(initialItems);
  const [error, setError] = useState(initialError);
  const [actionId, setActionId] = useState<string | null>(null);
  const [rejectId, setRejectId] = useState<string | null>(null);
  const [rejectReason, setRejectReason] = useState("");

  const reload = useCallback(async () => {
    setError(null);
    try {
      const response = await fetch("/api/admin/businesses/pending");
      if (!response.ok) {
        throw new Error("Failed to load queue");
      }
      const payload = (await response.json()) as { items: PendingBusiness[] };
      setItems(payload.items ?? []);
    } catch {
      setError("Could not load the verification queue.");
    }
  }, []);

  useEffect(() => {
    if (initialError) {
      void reload();
    }
  }, [initialError, reload]);

  async function handleApprove(tenantId: string) {
    setActionId(tenantId);
    setError(null);

    try {
      const response = await fetch(
        `/api/admin/businesses/${encodeURIComponent(tenantId)}/approve`,
        { method: "POST" },
      );
      if (!response.ok) {
        throw new Error("Approve failed");
      }
      setItems((current) => current.filter((item) => item.id !== tenantId));
    } catch {
      setError("Could not approve this business.");
    } finally {
      setActionId(null);
    }
  }

  async function handleReject(tenantId: string) {
    if (rejectReason.trim().length < 10) {
      setError("Rejection reason must be at least 10 characters.");
      return;
    }

    setActionId(tenantId);
    setError(null);

    try {
      const response = await fetch(
        `/api/admin/businesses/${encodeURIComponent(tenantId)}/reject`,
        {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ reason: rejectReason.trim() }),
        },
      );
      if (!response.ok) {
        throw new Error("Reject failed");
      }
      setItems((current) => current.filter((item) => item.id !== tenantId));
      setRejectId(null);
      setRejectReason("");
    } catch {
      setError("Could not reject this business.");
    } finally {
      setActionId(null);
    }
  }

  if (error) {
    return <p className="mt-4 text-sm text-red-800">{error}</p>;
  }

  if (items.length === 0) {
    return (
      <p className="mt-4 text-sm text-[#1b4332]/70">No businesses awaiting review.</p>
    );
  }

  return (
    <ul className="mt-4 divide-y divide-[#1b4332]/10 rounded-xl border border-[#1b4332]/10 bg-white">
      {items.map((business) => (
        <li key={business.id} className="px-5 py-4">
          <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
            <div>
              <p className="font-medium">{business.name}</p>
              <p className="text-sm text-[#1b4332]/60">
                {business.slug} · {business.marketId} · {business.status}
              </p>
              <time className="mt-1 block text-xs text-[#1b4332]/50">
                Submitted {new Date(business.createdAt).toLocaleDateString()}
              </time>
            </div>

            <div className="flex flex-col gap-2 sm:items-end">
              {rejectId === business.id ? (
                <div className="w-full max-w-md space-y-2">
                  <textarea
                    value={rejectReason}
                    onChange={(event) => setRejectReason(event.target.value)}
                    placeholder="Reason for rejection (min 10 characters)"
                    rows={3}
                    className="w-full rounded-lg border border-[#1b4332]/20 px-3 py-2 text-sm"
                  />
                  <div className="flex gap-2">
                    <button
                      type="button"
                      disabled={actionId === business.id}
                      onClick={() => void handleReject(business.id)}
                      className="rounded-full bg-[#1b4332] px-4 py-2 text-sm font-medium text-white disabled:opacity-60"
                    >
                      Confirm reject
                    </button>
                    <button
                      type="button"
                      onClick={() => {
                        setRejectId(null);
                        setRejectReason("");
                      }}
                      className="rounded-full border border-[#1b4332]/20 px-4 py-2 text-sm font-medium"
                    >
                      Cancel
                    </button>
                  </div>
                </div>
              ) : (
                <div className="flex gap-2">
                  <button
                    type="button"
                    disabled={actionId === business.id}
                    onClick={() => setRejectId(business.id)}
                    className="rounded-full border border-[#1b4332]/20 px-4 py-2 text-sm font-medium hover:bg-[#f6f8f6] disabled:opacity-60"
                  >
                    Reject
                  </button>
                  <button
                    type="button"
                    disabled={actionId === business.id}
                    onClick={() => void handleApprove(business.id)}
                    className="rounded-full bg-[#40916c] px-4 py-2 text-sm font-medium text-white hover:opacity-90 disabled:opacity-60"
                  >
                    {actionId === business.id ? "Saving…" : "Approve"}
                  </button>
                </div>
              )}
            </div>
          </div>
        </li>
      ))}
    </ul>
  );
}
