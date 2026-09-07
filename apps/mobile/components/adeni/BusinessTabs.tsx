import { Pressable, ScrollView, StyleSheet, Text } from "react-native";
import { useRouter, usePathname } from "expo-router";
import { SymbolView } from "expo-symbols";
import { hasCapability, type Capability } from "@adeni/shared";
import { adeniTheme } from "@/lib/theme";

const BUSINESS_TABS = [
  { href: "/business", label: "Overview", exact: true, ios: "square.grid.2x2", android: "dashboard" },
  { href: "/business/bookings", label: "Bookings", capability: "calendar" as Capability, ios: "calendar", android: "event" },
  { href: "/business/services", label: "Services", ios: "scissors", android: "content_cut" },
  { href: "/business/availability", label: "Hours", capability: "calendar" as Capability, ios: "clock", android: "schedule" },
  { href: "/business/payments", label: "Payments", capability: "deposits" as Capability, ios: "banknote", android: "payments" },
  { href: "/business/locations", label: "Locations", ios: "mappin", android: "place" },
  { href: "/business/profile", label: "Profile", ios: "person", android: "person" },
] as const;

type Props = {
  capabilities?: readonly string[];
};

export function BusinessTabs({ capabilities }: Props) {
  const router = useRouter();
  const pathname = usePathname();

  return (
    <ScrollView
      horizontal
      showsHorizontalScrollIndicator={false}
      contentContainerStyle={styles.row}
      style={styles.scroller}
    >
      {BUSINESS_TABS.filter((tab) => {
        if (!("capability" in tab) || !tab.capability || !capabilities) {
          return true;
        }

        return hasCapability(capabilities, tab.capability);
      }).map((tab) => {
        const active = "exact" in tab && tab.exact
          ? pathname === tab.href
          : pathname.startsWith(tab.href);

        return (
          <Pressable
            key={tab.href}
            onPress={() => !active && router.replace(tab.href)}
            style={({ pressed }) => [
              styles.pill,
              active && styles.pillActive,
              pressed && !active && styles.pillPressed,
            ]}
          >
            <SymbolView
              name={{ ios: tab.ios, android: tab.android, web: tab.android }}
              tintColor={active ? adeniTheme.primaryForeground : adeniTheme.textMuted}
              size={14}
            />
            <Text style={[styles.label, active && styles.labelActive]}>{tab.label}</Text>
          </Pressable>
        );
      })}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  scroller: {
    marginTop: adeniTheme.spacing.sm,
  },
  row: {
    paddingHorizontal: adeniTheme.spacing.xl,
    gap: adeniTheme.spacing.sm,
  },
  pill: {
    flexDirection: "row",
    alignItems: "center",
    gap: 6,
    borderRadius: adeniTheme.radius.full,
    borderWidth: 1,
    borderColor: adeniTheme.border,
    backgroundColor: adeniTheme.surface,
    paddingHorizontal: adeniTheme.spacing.lg,
    paddingVertical: adeniTheme.spacing.sm,
  },
  pillActive: {
    backgroundColor: adeniTheme.primary,
    borderColor: adeniTheme.primary,
  },
  pillPressed: {
    backgroundColor: adeniTheme.subtle,
  },
  label: {
    fontSize: 13,
    fontWeight: "600",
    color: adeniTheme.textMuted,
  },
  labelActive: {
    color: adeniTheme.primaryForeground,
  },
});
