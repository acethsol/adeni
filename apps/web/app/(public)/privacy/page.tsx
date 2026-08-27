import type { Metadata } from "next";
import { LegalDocumentPage } from "@/components/legal-document-page";

export const metadata: Metadata = {
  title: "Privacy Policy — Adeni",
  description: "How Adeni collects, uses, and protects personal data (draft placeholder).",
  robots: { index: false, follow: true },
};

export default function PrivacyPage() {
  return (
    <LegalDocumentPage
      title="Privacy Policy"
      description="How Adeni handles personal data for customers and businesses on our marketplace."
    >
      <h2>Overview</h2>
      <p>
        Adeni (&quot;we&quot;, &quot;us&quot;) operates a trusted local services marketplace in
        Lagos, Nigeria. This draft outlines topics our formal Privacy Policy will cover once
        approved by legal counsel.
      </p>

      <h2>Data we process</h2>
      <ul>
        <li>Account data via Auth0 (email, identity, roles)</li>
        <li>Business profiles, verification documents, and booking records</li>
        <li>Location used for discovery search (with your consent on device)</li>
        <li>Payment metadata via Paystack (Adeni does not hold funds)</li>
      </ul>

      <h2>Your rights (NDPR)</h2>
      <p>
        Subject to applicable law, you may request access, correction, or deletion of your
        personal data. Adeni provides admin-assisted export and erasure tools (SOC2-09) —
        the final policy will describe how to submit a request and expected timelines.
      </p>

      <h2>Data deletion</h2>
      <p>
        Customer erasure initiates a purge workflow (typically within 30 days). Some records
        may be retained where required for legal, fraud-prevention, or audit obligations.
      </p>

      <h2>Sub-processors</h2>
      <p>
        We use vetted providers including Auth0 (authentication), cloud hosting, and Paystack
        (payment orchestration). The published policy will list material sub-processors and
        regions.
      </p>

      <h2>Contact</h2>
      <p>
        Privacy contact email and Data Protection Officer details will appear here after legal
        review.
      </p>
    </LegalDocumentPage>
  );
}
