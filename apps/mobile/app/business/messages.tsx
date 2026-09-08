import { useCallback, useEffect, useState } from "react";
import {
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
} from "react-native";
import type { AdeniApiError } from "@adeni/api-client";
import type { MessageThreadDetail, MessageThreadSummary } from "@adeni/shared";
import { Screen, ScreenHeader } from "@/components/adeni/Screen";
import { BusinessTabs } from "@/components/adeni/BusinessTabs";
import { Button } from "@/components/ui/Button";
import { Callout } from "@/components/ui/Callout";
import { EmptyState } from "@/components/ui/EmptyState";
import { useAuth } from "@/contexts/auth-context";
import { isAuth0Configured } from "@/lib/auth/config";
import { adeniTheme } from "@/lib/theme";

function formatWhen(iso: string) {
  try {
    return new Date(iso).toLocaleString(undefined, {
      month: "short",
      day: "numeric",
      hour: "numeric",
      minute: "2-digit",
    });
  } catch {
    return iso;
  }
}

export default function BusinessMessagesScreen() {
  const {
    loading: authLoading,
    isBusinessInboxEnabled,
    createBusinessApiClient,
  } = useAuth();

  const [threads, setThreads] = useState<MessageThreadSummary[]>([]);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [detail, setDetail] = useState<MessageThreadDetail | null>(null);
  const [draft, setDraft] = useState("");
  const [loading, setLoading] = useState(true);
  const [sending, setSending] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const loadThreads = useCallback(async () => {
    setLoading(true);
    setError(null);

    try {
      const client = await createBusinessApiClient();
      const items = await client.listTenantMessageThreads();
      setThreads(items);
    } catch (err) {
      const apiError = err as AdeniApiError;
      if (apiError.statusCode === 403) {
        setError("Messaging requires a Pro plan. Upgrade from the Plan tab.");
      } else {
        setError("Could not load messages.");
      }
      setThreads([]);
    } finally {
      setLoading(false);
    }
  }, [createBusinessApiClient]);

  const loadDetail = useCallback(
    async (threadId: string) => {
      setError(null);
      try {
        const client = await createBusinessApiClient();
        const nextDetail = await client.getTenantMessageThread(threadId);
        setDetail(nextDetail);
        await client.markTenantThreadRead(threadId);
        setThreads((current) =>
          current.map((thread) =>
            thread.id === threadId ? { ...thread, unreadCount: 0 } : thread,
          ),
        );
      } catch {
        setError("Could not load conversation.");
      }
    },
    [createBusinessApiClient],
  );

  useEffect(() => {
    if (authLoading) {
      return;
    }

    if (isBusinessInboxEnabled) {
      void loadThreads();
      return;
    }

    setLoading(false);
  }, [authLoading, isBusinessInboxEnabled, loadThreads]);

  useEffect(() => {
    if (selectedId) {
      void loadDetail(selectedId);
    } else {
      setDetail(null);
    }
  }, [loadDetail, selectedId]);

  async function handleSend() {
    if (!selectedId || !draft.trim()) {
      return;
    }

    setSending(true);
    setError(null);

    try {
      const client = await createBusinessApiClient();
      await client.sendTenantMessage(selectedId, { body: draft.trim() });
      setDraft("");
      await loadDetail(selectedId);
      await loadThreads();
    } catch {
      setError("Could not send message.");
    } finally {
      setSending(false);
    }
  }

  const selected = threads.find((thread) => thread.id === selectedId) ?? null;

  return (
    <Screen loading={authLoading || loading}>
      <ScrollView contentContainerStyle={styles.content}>
        <ScreenHeader
          eyebrow="Business portal"
          title="Messages"
          subtitle="Reply to customers in-app."
        />

        {isBusinessInboxEnabled ? <BusinessTabs /> : null}

        {!isBusinessInboxEnabled ? (
          <Callout title="Sign in required">
            {isAuth0Configured()
              ? "Sign in with a business account to view messages."
              : "Set EXPO_PUBLIC_DEV_BUSINESS_AUTH0_SUB for local business mode."}
          </Callout>
        ) : null}

        {error ? <Callout title="Something went wrong">{error}</Callout> : null}

        {isBusinessInboxEnabled && threads.length === 0 && !selected ? (
          <EmptyState
            title="No messages yet"
            description="When customers message you on Adeni, conversations appear here."
          />
        ) : null}

        {isBusinessInboxEnabled && threads.length > 0 ? (
          <View style={styles.threadList}>
            {threads.map((thread) => (
              <Pressable
                key={thread.id}
                onPress={() => setSelectedId(thread.id)}
                style={[
                  styles.threadRow,
                  selectedId === thread.id && styles.threadRowActive,
                ]}
              >
                <View style={styles.threadHeader}>
                  <Text style={styles.threadName}>{thread.customerDisplayName}</Text>
                  {thread.unreadCount > 0 ? (
                    <View style={styles.unreadBadge}>
                      <Text style={styles.unreadText}>{thread.unreadCount}</Text>
                    </View>
                  ) : null}
                </View>
                {thread.preview ? (
                  <Text style={styles.preview} numberOfLines={2}>
                    {thread.preview}
                  </Text>
                ) : null}
                <Text style={styles.when}>{formatWhen(thread.lastMessageAt)}</Text>
              </Pressable>
            ))}
          </View>
        ) : null}

        {selected && detail ? (
          <View style={styles.conversation}>
            <Text style={styles.conversationTitle}>{selected.customerDisplayName}</Text>
            {detail.messages.map((message) => (
              <View
                key={message.id}
                style={[
                  styles.bubble,
                  message.senderType === "business" ? styles.bubbleBusiness : styles.bubbleCustomer,
                ]}
              >
                <Text
                  style={[
                    styles.bubbleText,
                    message.senderType === "business" && styles.bubbleTextBusiness,
                  ]}
                >
                  {message.body}
                </Text>
                <Text style={styles.bubbleWhen}>{formatWhen(message.createdAt)}</Text>
              </View>
            ))}

            <TextInput
              value={draft}
              onChangeText={setDraft}
              placeholder="Write a reply…"
              multiline
              style={styles.input}
            />
            <Button
              title={sending ? "Sending…" : "Send reply"}
              onPress={() => void handleSend()}
              disabled={sending || !draft.trim()}
            />
          </View>
        ) : null}
      </ScrollView>
    </Screen>
  );
}

const styles = StyleSheet.create({
  content: {
    padding: adeniTheme.spacing.xl,
    gap: adeniTheme.spacing.lg,
    paddingBottom: adeniTheme.spacing["3xl"],
  },
  threadList: {
    gap: adeniTheme.spacing.sm,
  },
  threadRow: {
    borderWidth: 1,
    borderColor: adeniTheme.border,
    borderRadius: adeniTheme.radius.lg,
    backgroundColor: adeniTheme.surface,
    padding: adeniTheme.spacing.lg,
  },
  threadRowActive: {
    borderColor: adeniTheme.primary,
    backgroundColor: adeniTheme.subtle,
  },
  threadHeader: {
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "space-between",
    gap: adeniTheme.spacing.sm,
  },
  threadName: {
    fontSize: 16,
    fontWeight: "700",
    color: adeniTheme.text,
  },
  unreadBadge: {
    backgroundColor: adeniTheme.destructive,
    borderRadius: adeniTheme.radius.full,
    paddingHorizontal: 8,
    paddingVertical: 2,
  },
  unreadText: {
    color: "#fff",
    fontSize: 11,
    fontWeight: "700",
  },
  preview: {
    marginTop: 4,
    fontSize: 13,
    color: adeniTheme.textMuted,
  },
  when: {
    marginTop: 4,
    fontSize: 11,
    color: adeniTheme.textMuted,
  },
  conversation: {
    gap: adeniTheme.spacing.md,
    marginTop: adeniTheme.spacing.md,
  },
  conversationTitle: {
    fontSize: 18,
    fontWeight: "700",
    color: adeniTheme.text,
  },
  bubble: {
    maxWidth: "85%",
    borderRadius: adeniTheme.radius.lg,
    padding: adeniTheme.spacing.md,
  },
  bubbleBusiness: {
    alignSelf: "flex-end",
    backgroundColor: adeniTheme.primary,
  },
  bubbleCustomer: {
    alignSelf: "flex-start",
    backgroundColor: adeniTheme.subtle,
  },
  bubbleText: {
    fontSize: 14,
    color: adeniTheme.text,
  },
  bubbleTextBusiness: {
    color: adeniTheme.primaryForeground,
  },
  bubbleWhen: {
    marginTop: 4,
    fontSize: 10,
    opacity: 0.7,
  },
  input: {
    borderWidth: 1,
    borderColor: adeniTheme.border,
    borderRadius: adeniTheme.radius.lg,
    padding: adeniTheme.spacing.md,
    minHeight: 88,
    textAlignVertical: "top",
    fontSize: 14,
    color: adeniTheme.text,
    backgroundColor: adeniTheme.surface,
  },
});
