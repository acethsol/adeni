import type { Metadata } from "next";
import { Geist, Geist_Mono } from "next/font/google";
import { MarketGeoSync } from "@/components/market-geo-sync";
import { LocaleProvider } from "@/components/locale-provider";
import { getLocale } from "@/lib/locale";
import { getTranslationPreference } from "@/lib/content-translation";
import { AppProviders } from "./providers";
import "./globals.css";

const geistSans = Geist({
  variable: "--font-geist-sans",
  subsets: ["latin"],
});

const geistMono = Geist_Mono({
  variable: "--font-geist-mono",
  subsets: ["latin"],
});

export const metadata: Metadata = {
  title: "Adeni",
  description: "Trusted local services marketplace",
};

export default async function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  const [locale, translateContent] = await Promise.all([
    getLocale(),
    getTranslationPreference(),
  ]);

  return (
    <html
      lang={locale}
      className={`${geistSans.variable} ${geistMono.variable} h-full antialiased`}
    >
      <body className="min-h-full flex flex-col">
        <AppProviders>
          <LocaleProvider locale={locale} translateContent={translateContent}>
            <MarketGeoSync />
            {children}
          </LocaleProvider>
        </AppProviders>
      </body>
    </html>
  );
}
