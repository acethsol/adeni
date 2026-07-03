import { useFonts } from "expo-font";
import { DarkTheme, DefaultTheme, Stack, ThemeProvider } from "expo-router";
import * as SplashScreen from "expo-splash-screen";
import { useEffect } from "react";
import "react-native-reanimated";

import { useColorScheme } from "@/components/useColorScheme";
import { AuthProvider } from "@/contexts/auth-context";
import { MarketProvider } from "@/contexts/market-context";
import { adeniTheme } from "@/lib/theme";

export { ErrorBoundary } from "expo-router";

export const unstable_settings = {
  initialRouteName: "(tabs)",
};

SplashScreen.preventAutoHideAsync();

const adeniNavigationTheme = {
  ...DefaultTheme,
  colors: {
    ...DefaultTheme.colors,
    background: adeniTheme.background,
    card: adeniTheme.surface,
    text: adeniTheme.text,
    primary: adeniTheme.primary,
    border: adeniTheme.border,
  },
};

export default function RootLayout() {
  const [loaded, error] = useFonts({
    SpaceMono: require("../assets/fonts/SpaceMono-Regular.ttf"),
  });

  useEffect(() => {
    if (error) throw error;
  }, [error]);

  useEffect(() => {
    if (loaded) {
      SplashScreen.hideAsync();
    }
  }, [loaded]);

  if (!loaded) {
    return null;
  }

  return <RootLayoutNav />;
}

function RootLayoutNav() {
  const colorScheme = useColorScheme();

  return (
    <AuthProvider>
      <MarketProvider>
        <ThemeProvider value={colorScheme === "dark" ? DarkTheme : adeniNavigationTheme}>
          <Stack>
            <Stack.Screen name="(tabs)" options={{ headerShown: false }} />
            <Stack.Screen
              name="business/[slug]"
              options={{
                headerShown: true,
                headerBackTitle: "Back",
              }}
            />
            <Stack.Screen
              name="business/bookings"
              options={{
                title: "Bookings",
                headerBackTitle: "Back",
              }}
            />
            <Stack.Screen name="modal" options={{ presentation: "modal" }} />
          </Stack>
        </ThemeProvider>
      </MarketProvider>
    </AuthProvider>
  );
}
