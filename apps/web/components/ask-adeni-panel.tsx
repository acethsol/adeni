"use client";

import { useRouter } from "next/navigation";
import { FormEvent, useState } from "react";
import { Sparkles } from "lucide-react";
import {
  ASK_ADENI_PROMPTS,
  buildDiscoverSearchParams,
  parseSearchIntent,
} from "@adeni/shared";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";

export function AskAdeniPanel() {
  const router = useRouter();
  const [prompt, setPrompt] = useState("");
  const [summary, setSummary] = useState<string | null>(null);

  function navigateWithIntent(text: string) {
    const intent = parseSearchIntent(text);
    const params = buildDiscoverSearchParams(intent);
    const search = new URLSearchParams();
    if (params.category) {
      search.set("category", params.category);
    }
    if (params.q) {
      search.set("q", params.q);
    }
    setSummary(intent.summary);
    router.push(`/discover${search.size > 0 ? `?${search.toString()}` : ""}`);
  }

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    if (!prompt.trim()) {
      return;
    }
    navigateWithIntent(prompt);
  }

  return (
    <Card padding="lg" className="border-accent/20 bg-gradient-to-br from-surface to-accent/5">
      <div className="flex items-start gap-3">
        <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-accent/10 text-accent">
          <Sparkles className="h-5 w-5" aria-hidden />
        </div>
        <div className="min-w-0 flex-1">
          <h2 className="text-lg font-semibold text-foreground">Ask Adeni</h2>
          <p className="mt-1 text-sm text-muted">
            Describe what you need in plain language — we&apos;ll find verified providers nearby.
          </p>
        </div>
      </div>

      <form onSubmit={handleSubmit} className="mt-5 space-y-3">
        <textarea
          value={prompt}
          onChange={(event) => setPrompt(event.target.value)}
          placeholder='e.g. "Barber in Lekki tomorrow" or "Braids near me"'
          rows={3}
          className="w-full resize-none rounded-xl border border-border-strong bg-surface px-4 py-3 text-sm text-foreground outline-none focus:border-accent focus:ring-2 focus:ring-accent/20"
        />
        <div className="flex flex-wrap gap-2">
          {ASK_ADENI_PROMPTS.map((example) => (
            <button
              key={example}
              type="button"
              onClick={() => {
                setPrompt(example);
                navigateWithIntent(example);
              }}
              className="rounded-full border border-border bg-surface px-3 py-1.5 text-xs font-semibold text-muted transition-colors hover:border-accent/40 hover:text-foreground"
            >
              {example}
            </button>
          ))}
        </div>
        <Button type="submit" disabled={!prompt.trim()}>
          Find services
        </Button>
        {summary ? <p className="text-sm text-accent">{summary}</p> : null}
      </form>
    </Card>
  );
}
