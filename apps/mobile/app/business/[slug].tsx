import { useEffect, useState } from "react";

import { Pressable, ScrollView, StyleSheet, Text, View } from "react-native";

import { Stack, useLocalSearchParams, useRouter } from "expo-router";

import type { PublicBusinessProfile, ServiceOffering } from "@adeni/shared";

import { BookingPanel } from "@/components/adeni/BookingPanel";

import { Screen } from "@/components/adeni/Screen";
import { createPublicApiClient } from "@/lib/api";

import { adeniTheme } from "@/lib/theme";



export default function BusinessProfileScreen() {
  const router = useRouter();
  const { slug } = useLocalSearchParams<{ slug: string }>();

  const [profile, setProfile] = useState<PublicBusinessProfile | null>(null);

  const [services, setServices] = useState<ServiceOffering[]>([]);

  const [loading, setLoading] = useState(true);

  const [error, setError] = useState<string | null>(null);



  useEffect(() => {

    if (!slug) {

      setError("Business not found.");

      setLoading(false);

      return;

    }



    let cancelled = false;



    async function load() {

      setLoading(true);

      setError(null);



      try {

        const client = createPublicApiClient();

        const [business, serviceItems] = await Promise.all([

          client.getBusinessProfile(slug),

          client.getBusinessServices(slug).catch(() => []),

        ]);



        if (!cancelled) {

          setProfile(business);

          setServices(serviceItems);

        }

      } catch {

        if (!cancelled) {

          setError("Could not load this business.");

          setProfile(null);

        }

      } finally {

        if (!cancelled) {

          setLoading(false);

        }

      }

    }



    void load();



    return () => {

      cancelled = true;

    };

  }, [slug]);



  return (

    <>

      <Stack.Screen options={{ title: profile?.name ?? "Business" }} />

      <Screen loading={loading} error={error}>

        {profile ? (

          <ScrollView contentContainerStyle={styles.content}>

            <Pressable onPress={() => router.back()}>

              <Text style={styles.backLink}>← Back</Text>

            </Pressable>



            <View style={styles.card}>

              <Text style={styles.eyebrow}>Verified business</Text>

              <Text style={styles.name}>{profile.name}</Text>

              <Text style={styles.meta}>

                {profile.locationName !== profile.name ? `${profile.locationName} · ` : ""}

                {profile.area} · {profile.categorySlug.replace(/-/g, " ")}

              </Text>



              {profile.description ? (

                <Text style={styles.description}>{profile.description}</Text>

              ) : null}



              <View style={styles.details}>

                <Detail label="Address" value={profile.addressLine} />

                <Detail label="Phone" value={profile.phoneMasked} />

              </View>

            </View>



            <BookingPanel slug={profile.slug} tenantId={profile.tenantId} services={services} />

          </ScrollView>

        ) : null}

      </Screen>

    </>

  );

}



function Detail({ label, value }: { label: string; value: string }) {

  return (

    <View style={styles.detailBlock}>

      <Text style={styles.detailLabel}>{label}</Text>

      <Text style={styles.detailValue}>{value}</Text>

    </View>

  );

}



const styles = StyleSheet.create({

  content: {

    paddingHorizontal: 20,

    paddingBottom: 32,

  },

  backLink: {

    fontSize: 14,

    fontWeight: "600",

    color: adeniTheme.accent,

    marginBottom: 12,

  },

  card: {

    backgroundColor: adeniTheme.surface,

    borderRadius: 16,

    borderWidth: 1,

    borderColor: adeniTheme.border,

    padding: 20,

  },

  eyebrow: {

    fontSize: 12,

    fontWeight: "700",

    letterSpacing: 1,

    textTransform: "uppercase",

    color: adeniTheme.accent,

  },

  name: {

    marginTop: 8,

    fontSize: 26,

    fontWeight: "700",

    color: adeniTheme.text,

  },

  meta: {

    marginTop: 8,

    fontSize: 15,

    color: adeniTheme.textMuted,

    textTransform: "capitalize",

  },

  description: {

    marginTop: 16,

    fontSize: 15,

    lineHeight: 22,

    color: adeniTheme.text,

  },

  details: {

    marginTop: 20,

    gap: 14,

  },

  detailBlock: {},

  detailLabel: {

    fontSize: 13,

    fontWeight: "600",

    color: adeniTheme.textSubtle,

  },

  detailValue: {

    marginTop: 4,

    fontSize: 15,

    color: adeniTheme.text,

  },

});


