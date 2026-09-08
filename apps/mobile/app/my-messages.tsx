import { useCallback, useEffect, useState } from "react";
import {
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
} from "react-native";
import { useRouter } from "expo-router";
import type { AdeniApiError } from "@adeni/api-client";
import type { MessageThreadDetail, MessageThreadSummary } from "@adeni/shared";
import { formatWhen } from "@adeni/shared";
import { Screen, ScreenHeader } from "@/components/adeni/Screen";
import { Button } from "@/components/ui/Button";
import { Callout } from "@/components/ui/Callout";
import { EmptyState } from "@/components/ui/EmptyState";
import { useAuth } from "@/contexts/auth-context";
import { isAuth0Configured } from "@/lib/auth/config";
import { adeniTheme } from "@/lib/theme";

export default function MyMessagesScreen() {
  const router = useRouter();
  const { loading: authLoading, isBookingEnabled, createApiClient } = useAuth();

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
      const client = createApiClient("customer");
      const items = await client.listCustomerMessageThreads();
      setThreads(items);
    } catch (err) {
      const apiError = err as AdeniApiError;
      if (apiError.statusCode === 401) {
        setError("Sign in to view your messages.");
      } else {
        setError("Could not load messages.");
      }
      setThreads([]);
    } finally {
      setLoading(false);
    }
  }, [createApiClient]);

  const loadDetail = useCallback(
    async (threadId: string) => {
      setError(null);
      try {
        const client = createApiClient("customer");
        const nextDetail = await client.getCustomerMessageThread(threadId);
        setDetail(nextDetail);
        await client.markCustomerThreadRead(threadId);
        setThreads((current) =>
          current.map((thread) =>
            thread.id === threadId ? { ...thread, unreadCount: 0 } : thread,
          ),
        );
      } catch {
        setError("Could not load conversation.");
      }
    },
    [createApiClient],
  );

  useEffect(() => {
    if (authLoading) {
      return;
    }

    if (isBookingEnabled) {
      void loadThreads();
      return;
    }

    setLoading(false);
  }, [authLoading, isBookingEnabled, loadThreads]);

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
      const client = createApiClient("customer");
      await client.sendCustomerMessage(selectedId, { body: draft.trim() });
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
          eyebrow="Customer"
          title="Messages"
          subtitle="Chat with businesses on Adeni."
        />

        {!isBookingEnabled ? (
          <Callout title="Sign in required">
            {isAuth0Configured()
              ? "Sign in from the Account tab to view messages."
              : "Set EXPO_PUBLIC_DEV_CUSTOMER_AUTH0_SUB for local customer mode."}
          </Callout>
        ) : null}

        {error ? <Callout title="Something went wrong">{error}</Callout> : null}

        {isBookingEnabled && threads.length === 0 && !selected ? (
          <EmptyState
            title="No conversations yet"
            description="Message a business from their profile to start chatting."
            actionLabel="Discover businesses"
            onAction={() => router.push("/discover")}
          />
        ) : null}

        {isBookingEnabled && threads.length > 0 ? (
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
                  <Text style={styles.threadName}>{thread.businessName ?? "Business"}</Text>
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
            <Text style={styles.conversationTitle}>{selected.businessName ?? "Business"}</Text>
            {detail.messages.map((message) => (
              <View
                key={message.id}
                style={[
                  styles.bubble,
                  message.senderType === "customer" ? styles.bubbleCustomer : styles.bubbleBusiness,
                ]}
              >
                <Text
                  style={[
                    styles.bubbleText,
                    message.senderType === "customer" && styles.bubbleTextCustomer,
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
              placeholder="Write a message…"
              multiline
              style={styles.input}
            />
            <Button
              title={sending ? "Sending…" : "Send message"}
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
  bubbleCustomer: {
    alignSelf: "flex-end",
    backgroundColor: adeniTheme.primary,
  },
  bubbleBusiness: {
    alignSelf: "flex-start",
    backgroundColor: adeniTheme.subtle,
  },
  bubbleText: {
    fontSize: 14,
    color: adeniTheme.text,
  },
  bubbleTextCustomer: {
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
