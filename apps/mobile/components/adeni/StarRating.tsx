import { StyleSheet, Text, View } from "react-native";
import { adeniTheme } from "@/lib/theme";

type Props = {
  rating: number;
  size?: "sm" | "md" | "lg";
};

const sizeMap = {
  sm: 12,
  md: 16,
  lg: 20,
};

export function StarRating({ rating, size = "sm" }: Props) {
  const fontSize = sizeMap[size];
  const filled = Math.round(Math.max(0, Math.min(5, rating)));

  return (
    <View style={styles.row} accessibilityLabel={`${rating} out of 5 stars`}>
      {Array.from({ length: 5 }, (_, index) => (
        <Text
          key={index}
          style={[
            styles.star,
            { fontSize },
            index < filled ? styles.starFilled : styles.starEmpty,
          ]}
        >
          ★
        </Text>
      ))}
    </View>
  );
}

const styles = StyleSheet.create({
  row: {
    flexDirection: "row",
    alignItems: "center",
    gap: 2,
  },
  star: {
    lineHeight: undefined,
  },
  starFilled: {
    color: "#fbbf24",
  },
  starEmpty: {
    color: adeniTheme.borderStrong,
  },
});
