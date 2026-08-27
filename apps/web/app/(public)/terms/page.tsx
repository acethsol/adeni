import type { Metadata } from "next";
import { LegalDocumentPage } from "@/components/legal-document-page";

export const metadata: Metadata = {
  title: "Terms of Service — Adeni",
  description: "Terms governing use of the Adeni marketplace (draft placeholder).",
  robots: { index: false, follow: true },
};

export default function TermsPage() {
  return (
    <LegalDocumentPage
      title="Terms of Service"
      description="Rules for using Adeni as a customer or business on our marketplace."
    >
      <h2>Agreement</h2>
      <p>
        By using Adeni you agree to these Terms and our Privacy Policy. This draft placeholder
        summarises clauses our lawyers will formalise before public launch.
      </p>

      <h2>Marketplace role</h2>
      <p>
        Adeni provides discovery, booking, and business tools. Service delivery is performed by
        independent businesses listed on the platform. Adeni orchestrates payments through
        licensed providers and does not operate a wallet or hold customer funds.
      </p>

      <h2>Acceptable use</h2>
      <ul>
        <li>Provide accurate information when registering or booking</li>
        <li>Do not misuse verification, reviews, or messaging features</li>
        <li>Comply with applicable Nigerian laws and industry regulations</li>
      </ul>

      <h2>Limitation of liability</h2>
      <p>
        To the maximum extent permitted by law, Adeni&apos;s liability for platform use will be
        limited as set out in the final Terms. Businesses are responsible for the quality and
        safety of services they provide.
      </p>

      <h2>Indemnification</h2>
      <p>
        Businesses may be required to indemnify Adeni against claims arising from their listings,
        conduct, or breach of these Terms. Wording will be confirmed by counsel.
      </p>

      <h2>Governing law</h2>
      <p>
        The final Terms will specify governing law and dispute resolution (expected: Federal
        Republic of Nigeria, subject to legal advice).
      </p>

      <h2>Changes</h2>
      <p>
        We may update these Terms with notice as described in the published version. Material
        changes affecting businesses will be communicated through the portal or email.
      </p>
    </LegalDocumentPage>
  );
}
