import Link from "next/link";
import { Globe } from "lucide-react";
import { cn } from "@/lib/cn";

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
};

export function PublicFooter({ className }: Props) {
  const year = new Date().getFullYear();

  return (
    <footer className={cn("mt-auto border-t border-border bg-[#eef2ef]", className)}>
      <div className="mx-auto max-w-5xl px-6 py-12">
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

          <div className="flex flex-wrap items-center gap-3">
            <button
              type="button"
              className="inline-flex items-center gap-2 rounded-lg border border-border bg-surface px-3 py-2 text-sm font-semibold text-foreground shadow-sm transition-colors hover:border-border-strong"
              aria-label="Language and region"
            >
              <Globe className="h-4 w-4" aria-hidden />
              English
            </button>
            <button
              type="button"
              className="inline-flex items-center gap-2 rounded-lg border border-border bg-surface px-3 py-2 text-sm font-semibold text-foreground shadow-sm transition-colors hover:border-border-strong"
              aria-label="Currency"
            >
              ₦ NGN
            </button>
          </div>
        </div>
      </div>
    </footer>
  );
}
