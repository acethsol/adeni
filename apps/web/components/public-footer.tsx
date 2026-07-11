import Link from "next/link";
import { cn } from "@/lib/cn";
import { publicContainerClass } from "@/lib/layout-classes";
import { LocaleCurrencySwitcher } from "@/components/locale-currency-switcher";

type FooterLink = {
  label: string;
  href: string;
};

type FooterSection = {
  title: string;
  links: FooterLink[];
};

const sections: FooterSection[] = [
  {
    title: "Support",
    links: [
      { label: "Help centre", href: "/discover" },
      { label: "Trust & verification", href: "/discover" },
      { label: "Booking help", href: "/my-bookings" },
      { label: "Contact us", href: "/discover" },
    ],
  },
  {
    title: "Discover",
    links: [
      { label: "Browse services", href: "/discover" },
      { label: "Beauty & grooming", href: "/discover?category=barbers" },
      { label: "Home services", href: "/discover?category=plumbers" },
      { label: "Verified providers", href: "/discover" },
    ],
  },
  {
    title: "For business",
    links: [
      { label: "List your business", href: "/business/register" },
      { label: "Business portal", href: "/business" },
      { label: "Get verified", href: "/business/profile" },
      { label: "Manage bookings", href: "/business/bookings" },
    ],
  },
  {
    title: "Adeni",
    links: [
      { label: "How it works", href: "/" },
      { label: "About", href: "/" },
      { label: "Privacy", href: "/" },
      { label: "Terms", href: "/" },
    ],
  },
];

const legalLinks: FooterLink[] = [
  { label: "Privacy", href: "/" },
  { label: "Terms", href: "/" },
  { label: "Sitemap", href: "/discover" },
];

type Props = {
  className?: string;
  marketId: string;
  currency: string;
  countryCode: string;
};

export function PublicFooter({ className, marketId, currency, countryCode }: Props) {
  const year = new Date().getFullYear();

  return (
    <footer className={cn("mt-auto border-t border-border bg-subtle", className)}>
      <div className={`${publicContainerClass} py-12`}>
        <div className="grid gap-10 sm:grid-cols-2 lg:grid-cols-4">
          {sections.map((section) => (
            <div key={section.title}>
              <h2 className="text-sm font-semibold text-foreground">{section.title}</h2>
              <ul className="mt-4 space-y-3">
                {section.links.map((link) => (
                  <li key={`${section.title}-${link.label}`}>
                    <Link
                      href={link.href}
                      className="text-sm text-muted transition-colors hover:text-foreground hover:underline"
                    >
                      {link.label}
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
              <span key={link.label} className="inline-flex items-center gap-2">
                <Link href={link.href} className="hover:text-foreground hover:underline">
                  {link.label}
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
          />
        </div>
      </div>
    </footer>
  );
}
