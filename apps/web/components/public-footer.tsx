import Link from "next/link";
import { buildLocaleRegionPresets, t } from "@adeni/shared";
import { cn } from "@/lib/cn";
import { publicContainerClass } from "@/lib/layout-classes";
import { getLocale } from "@/lib/locale";
import { LocaleCurrencySwitcher } from "@/components/locale-currency-switcher";
import { getMarkets } from "@/lib/markets-api";

type FooterLink = {
  labelKey: string;
  href: string;
};

type FooterSection = {
  titleKey: string;
  links: FooterLink[];
};

const sections: FooterSection[] = [
  {
    titleKey: "footer.sections.support",
    links: [
      { labelKey: "footer.links.helpCentre", href: "/discover" },
      { labelKey: "footer.links.trustVerification", href: "/discover" },
      { labelKey: "footer.links.bookingHelp", href: "/my-bookings" },
      { labelKey: "footer.links.contactUs", href: "/discover" },
    ],
  },
  {
    titleKey: "footer.sections.discover",
    links: [
      { labelKey: "footer.links.browseServices", href: "/discover" },
      { labelKey: "footer.links.beautyGrooming", href: "/discover?category=barbers" },
      { labelKey: "footer.links.homeServices", href: "/discover?category=plumbers" },
      { labelKey: "footer.links.verifiedProviders", href: "/discover" },
    ],
  },
  {
    titleKey: "footer.sections.business",
    links: [
      { labelKey: "footer.links.listBusiness", href: "/business/register" },
      { labelKey: "footer.links.businessPortal", href: "/business" },
      { labelKey: "footer.links.getVerified", href: "/business/profile" },
      { labelKey: "footer.links.manageBookings", href: "/business/bookings" },
    ],
  },
  {
    titleKey: "footer.sections.adeni",
    links: [
      { labelKey: "footer.links.howItWorks", href: "/" },
      { labelKey: "footer.links.about", href: "/" },
      { labelKey: "footer.links.privacy", href: "/" },
      { labelKey: "footer.links.terms", href: "/" },
    ],
  },
];

const legalLinks: FooterLink[] = [
  { labelKey: "footer.links.privacy", href: "/" },
  { labelKey: "footer.links.terms", href: "/" },
  { labelKey: "footer.links.sitemap", href: "/discover" },
];

type Props = {
  className?: string;
  marketId: string;
  currency: string;
  countryCode: string;
};

export async function PublicFooter({ className, marketId, currency, countryCode }: Props) {
  const [locale, markets] = await Promise.all([getLocale(), getMarkets()]);
  const presets = buildLocaleRegionPresets(markets);
  const year = new Date().getFullYear();

  return (
    <footer className={cn("mt-auto border-t border-border bg-subtle", className)}>
      <div className={`${publicContainerClass} py-12`}>
        <div className="grid gap-10 sm:grid-cols-2 lg:grid-cols-4">
          {sections.map((section) => (
            <div key={section.titleKey}>
              <h2 className="text-sm font-semibold text-foreground">
                {t(locale, section.titleKey)}
              </h2>
              <ul className="mt-4 space-y-3">
                {section.links.map((link) => (
                  <li key={`${section.titleKey}-${link.labelKey}`}>
                    <Link
                      href={link.href}
                      className="text-sm text-muted transition-colors hover:text-foreground hover:underline"
                    >
                      {t(locale, link.labelKey)}
                    </Link>
                  </li>
                ))}
              </ul>
            </div>
          ))}
        </div>

        <div className="mt-10 flex flex-col gap-4 border-t border-border pt-8 sm:flex-row sm:items-center sm:justify-between">
          <div className="flex flex-wrap items-center gap-x-2 gap-y-1 text-sm text-muted">
            <span>© {year} Adeni</span>
            <span aria-hidden>·</span>
            {legalLinks.map((link, index) => (
              <span key={link.labelKey} className="inline-flex items-center gap-2">
                <Link href={link.href} className="hover:text-foreground hover:underline">
                  {t(locale, link.labelKey)}
                </Link>
                {index < legalLinks.length - 1 ? <span aria-hidden>·</span> : null}
              </span>
            ))}
          </div>

          <LocaleCurrencySwitcher
            currentMarketId={marketId}
            currentCurrency={currency}
            countryCode={countryCode}
            mode="footer"
            presets={presets}
          />
        </div>
      </div>
    </footer>
  );
}
