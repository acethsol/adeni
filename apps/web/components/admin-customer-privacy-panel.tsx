"use client";

import { useState } from "react";
import type { AdminCustomerSummary, CustomerDataExport } from "@adeni/shared";

export function AdminCustomerPrivacyPanel() {
  const [email, setEmail] = useState("");
  const [searching, setSearching] = useState(false);
  const [customers, setCustomers] = useState<AdminCustomerSummary[]>([]);
  const [exportData, setExportData] = useState<CustomerDataExport | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);

  async function handleSearch(event: React.FormEvent) {
    event.preventDefault();
    setSearching(true);
    setError(null);
    setMessage(null);
    setExportData(null);

    try {
      const query = new URLSearchParams({ email: email.trim() });
      const response = await fetch(`/api/admin/customers?${query.toString()}`);
      if (!response.ok) {
        throw new Error("Search failed");
      }
      const payload = (await response.json()) as { items: AdminCustomerSummary[] };
      setCustomers(payload.items ?? []);
    } catch {
      setError("Could not search customers.");
      setCustomers([]);
    } finally {
      setSearching(false);
    }
  }

  async function handleExport(customerId: string) {
    setBusyId(customerId);
    setError(null);
    setMessage(null);

    try {
      const response = await fetch(
        `/api/admin/customers/${encodeURIComponent(customerId)}/export`,
      );
      if (!response.ok) {
        throw new Error("Export failed");
      }
      setExportData((await response.json()) as CustomerDataExport);
      setMessage("Customer data exported. Download JSON below.");
    } catch {
      setError("Could not export customer data.");
    } finally {
      setBusyId(null);
    }
  }

  async function handleDelete(customerId: string) {
    if (!window.confirm("Initiate erasure for this customer? PII will be cleared immediately.")) {
      return;
    }

    setBusyId(customerId);
    setError(null);
    setMessage(null);

    try {
      const response = await fetch(
        `/api/admin/customers/${encodeURIComponent(customerId)}/delete`,
        { method: "POST" },
      );
      if (!response.ok) {
        throw new Error("Delete failed");
      }
      setMessage("Erasure initiated for this customer.");
      setCustomers((current) =>
        current.map((item) =>
          item.id === customerId
            ? { ...item, name: "[erased]", email: null, erasureRequestedAt: new Date().toISOString() }
            : item,
        ),
      );
    } catch {
      setError("Could not initiate customer erasure.");
    } finally {
      setBusyId(null);
    }
  }

  return (
    <section className="mt-10">
      <h2 className="text-lg font-semibold">Customer privacy (SOC2-09)</h2>
      <p className="mt-2 text-sm text-[#1b4332]/70">
        Search by email to export a subject access bundle or initiate erasure.
      </p>

      <form onSubmit={(event) => void handleSearch(event)} className="mt-4 flex flex-wrap gap-3">
        <input
          type="email"
          value={email}
          onChange={(event) => setEmail(event.target.value)}
          placeholder="customer@example.com"
          required
          className="min-w-[240px] flex-1 rounded-lg border border-[#1b4332]/20 px-3 py-2 text-sm"
        />
        <button
          type="submit"
          disabled={searching}
          className="rounded-full bg-[#1b4332] px-5 py-2 text-sm font-medium text-white disabled:opacity-60"
        >
          {searching ? "Searching…" : "Search"}
        </button>
      </form>

      {error ? <p className="mt-4 text-sm text-red-800">{error}</p> : null}
      {message ? <p className="mt-4 text-sm text-[#40916c]">{message}</p> : null}

      {customers.length > 0 ? (
        <ul className="mt-4 divide-y divide-[#1b4332]/10 rounded-xl border border-[#1b4332]/10 bg-white">
          {customers.map((customer) => (
            <li key={customer.id} className="flex flex-col gap-3 px-5 py-4 sm:flex-row sm:items-center sm:justify-between">
              <div>
                <p className="font-medium">{customer.name || "Customer"}</p>
                <p className="text-sm text-[#1b4332]/60">
                  {customer.email ?? "No email"} · {customer.id}
                </p>
                {customer.erasureRequestedAt ? (
                  <p className="mt-1 text-xs font-medium text-amber-700">
                    Erasure requested {new Date(customer.erasureRequestedAt).toLocaleDateString()}
                  </p>
                ) : null}
              </div>
              <div className="flex gap-2">
                <button
                  type="button"
                  disabled={busyId === customer.id}
                  onClick={() => void handleExport(customer.id)}
                  className="rounded-full border border-[#1b4332]/20 px-4 py-2 text-sm font-medium disabled:opacity-60"
                >
                  Export
                </button>
                <button
                  type="button"
                  disabled={busyId === customer.id || Boolean(customer.erasureRequestedAt)}
                  onClick={() => void handleDelete(customer.id)}
                  className="rounded-full border border-red-200 px-4 py-2 text-sm font-medium text-red-800 disabled:opacity-60"
                >
                  Initiate erasure
                </button>
              </div>
            </li>
          ))}
        </ul>
      ) : null}

      {exportData ? (
        <div className="mt-6 rounded-xl border border-[#1b4332]/10 bg-white p-5">
          <div className="flex items-center justify-between gap-4">
            <h3 className="font-semibold">Export bundle</h3>
            <a
              href={`data:application/json;charset=utf-8,${encodeURIComponent(JSON.stringify(exportData, null, 2))}`}
              download={`customer-${exportData.customerId}.json`}
              className="rounded-full border border-[#1b4332]/20 px-4 py-2 text-sm font-medium"
            >
              Download JSON
            </a>
          </div>
          <pre className="mt-4 max-h-80 overflow-auto rounded-lg bg-[#f6f8f6] p-4 text-xs">
            {JSON.stringify(exportData, null, 2)}
          </pre>
        </div>
      ) : null}
    </section>
  );
}
