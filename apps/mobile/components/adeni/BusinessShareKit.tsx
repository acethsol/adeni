import { Linking, Share, StyleSheet, Text, View } from "react-native";
import { Card } from "@/components/ui/Card";
import { Button } from "@/components/ui/Button";
import { getWebBaseUrl } from "@/lib/env";
import { adeniTheme } from "@/lib/theme";

type Props = {
  businessName: string;
  slug: string;
};

function buildPublicUrl(slug: string) {
  const base = getWebBaseUrl().replace(/\/$/, "");
  return `${base}/businesses/${slug}`;
}

export function BusinessShareKit({ businessName, slug }: Props) {
  const publicUrl = buildPublicUrl(slug);
  const whatsAppMessage = encodeURIComponent(
    `Book ${businessName} on Adeni — pick a service and time:\n${publicUrl}`,
  );
  const whatsAppHref = `https://wa.me/?text=${whatsAppMessage}`;
  const instagramBio = `${businessName} — book online:\n${publicUrl}\n\nAdd this link to your Instagram bio or story.`;

  return (
    <Card title="Share kit" description="Copy your public booking link or share templates for WhatsApp and Instagram.">
      <Text style={styles.sectionLabel}>Public link</Text>
      <Text style={styles.url} selectable>
        {publicUrl}
      </Text>

      <View style={styles.actions}>
        <Button
          title="Share link"
          variant="secondary"
          onPress={() =>
            void Share.share({
              message: `Book ${businessName} on Adeni: ${publicUrl}`,
            })
          }
        />
        <Button
          title="WhatsApp"
          onPress={() => void Linking.openURL(whatsAppHref)}
          containerStyle={styles.whatsAppButton}
        />
        <Button
          title="Instagram bio"
          variant="secondary"
          onPress={() => void Share.share({ message: instagramBio })}
        />
      </View>
    </Card>
  );
}

const styles = StyleSheet.create({
  sectionLabel: {
    marginTop: adeniTheme.spacing.lg,
    fontSize: 11,
    fontWeight: "700",
    letterSpacing: 0.8,
    textTransform: "uppercase",
    color: adeniTheme.accent,
  },
  url: {
    marginTop: adeniTheme.spacing.sm,
    borderRadius: adeniTheme.radius.md,
    backgroundColor: adeniTheme.subtle,
    paddingHorizontal: adeniTheme.spacing.md,
    paddingVertical: adeniTheme.spacing.md,
    fontSize: 14,
    lineHeight: 20,
    color: adeniTheme.text,
  },
  actions: {
    marginTop: adeniTheme.spacing.lg,
    flexDirection: "row",
    flexWrap: "wrap",
    gap: adeniTheme.spacing.sm,
  },
  whatsAppButton: {
    backgroundColor: "#25D366",
  },
});
