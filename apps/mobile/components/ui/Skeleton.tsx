import { useEffect, useRef } from "react";
import { Animated, StyleSheet, View, type ViewStyle } from "react-native";
import { adeniTheme } from "@/lib/theme";

export function Skeleton({ style }: { style?: ViewStyle }) {
  const opacity = useRef(new Animated.Value(0.45)).current;

  useEffect(() => {
    const animation = Animated.loop(
      Animated.sequence([
        Animated.timing(opacity, { toValue: 1, duration: 750, useNativeDriver: true }),
        Animated.timing(opacity, { toValue: 0.45, duration: 750, useNativeDriver: true }),
      ]),
    );
    animation.start();
    return () => animation.stop();
  }, [opacity]);

  return <Animated.View style={[styles.skeleton, { opacity }, style]} />;
}

export function SkeletonCard() {
  return (
    <View style={styles.card}>
      <Skeleton style={styles.lineShort} />
      <Skeleton style={styles.lineFull} />
      <Skeleton style={styles.lineMedium} />
    </View>
  );
}

export function SkeletonList({ count = 3 }: { count?: number }) {
  return (
    <View style={styles.list}>
      {Array.from({ length: count }).map((_, index) => (
        <SkeletonCard key={index} />
      ))}
    </View>
  );
}

const styles = StyleSheet.create({
  skeleton: {
    backgroundColor: "rgba(27, 67, 50, 0.1)",
    borderRadius: adeniTheme.radius.sm,
  },
  card: {
    borderRadius: adeniTheme.radius.lg,
    borderWidth: 1,
    borderColor: adeniTheme.border,
    backgroundColor: adeniTheme.surface,
    padding: adeniTheme.spacing.xl,
    gap: adeniTheme.spacing.sm,
  },
  lineShort: { height: 16, width: "35%" },
  lineFull: { height: 14, width: "100%" },
  lineMedium: { height: 14, width: "70%" },
  list: { gap: adeniTheme.spacing.md },
});
