import { StyleSheet } from "react-native";
import { getMarket } from "@adeni/shared";
import { Text, View } from "@/components/Themed";

const market = getMarket();

export default function HomeScreen() {
  return (
    <View style={styles.container}>
      <Text style={styles.brand}>Adeni</Text>
      <Text style={styles.subtitle}>
        {market.tagline} in {market.name}
      </Text>
      <Text style={styles.body}>{market.description}</Text>
      <Text style={styles.note}>{market.launchNote}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    alignItems: "center",
    justifyContent: "center",
    padding: 24,
  },
  brand: {
    fontSize: 32,
    fontWeight: "bold",
    color: "#1b4332",
  },
  subtitle: {
    marginTop: 8,
    fontSize: 16,
    opacity: 0.8,
  },
  body: {
    marginTop: 16,
    textAlign: "center",
    lineHeight: 22,
    opacity: 0.75,
  },
  note: {
    marginTop: 12,
    textAlign: "center",
    fontSize: 14,
    lineHeight: 20,
    opacity: 0.6,
  },
});
