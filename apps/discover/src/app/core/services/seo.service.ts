import { DOCUMENT, inject, Injectable } from "@angular/core";
import { Meta, Title } from "@angular/platform-browser";

export type SeoTags = {
  title: string;
  description?: string;
  canonicalPath?: string;
  imageUrl?: string | null;
  jsonLd?: Record<string, unknown> | null;
};

@Injectable({ providedIn: "root" })
export class SeoService {
  private readonly title = inject(Title);
  private readonly meta = inject(Meta);
  private readonly document = inject(DOCUMENT);

  private jsonLdElementId = "adeni-json-ld";

  update(tags: SeoTags, publicOrigin: string): void {
    this.title.setTitle(tags.title);

    this.setMeta("description", tags.description);
    this.setMeta("og:title", tags.title);
    this.setMeta("og:description", tags.description);
    this.setMeta("og:type", "website");

    if (tags.canonicalPath) {
      const url = `${publicOrigin}${tags.canonicalPath.startsWith("/") ? tags.canonicalPath : `/${tags.canonicalPath}`}`;
      this.setMeta("og:url", url);
      this.setLinkCanonical(url);
    }

    if (tags.imageUrl) {
      this.setMeta("og:image", tags.imageUrl);
      this.setMeta("twitter:card", "summary_large_image");
      this.setMeta("twitter:image", tags.imageUrl);
    } else {
      this.meta.removeTag("name='twitter:card'");
    }

    this.setJsonLd(tags.jsonLd);
  }

  private setMeta(name: string, content: string | undefined): void {
    if (!content) {
      this.meta.removeTag(`name='${name}'`);
      this.meta.removeTag(`property='${name}'`);
      return;
    }

    if (name.startsWith("og:") || name.startsWith("twitter:")) {
      this.meta.updateTag({ property: name, content });
    } else {
      this.meta.updateTag({ name, content });
    }
  }

  private setLinkCanonical(href: string): void {
    const head = this.document.head;
    let link = head.querySelector<HTMLLinkElement>("link[rel='canonical']");
    if (!link) {
      link = this.document.createElement("link");
      link.setAttribute("rel", "canonical");
      head.appendChild(link);
    }
    link.setAttribute("href", href);
  }

  private setJsonLd(jsonLd: Record<string, unknown> | null | undefined): void {
    const existing = this.document.getElementById(this.jsonLdElementId);
    existing?.remove();

    if (!jsonLd) {
      return;
    }

    const script = this.document.createElement("script");
    script.id = this.jsonLdElementId;
    script.type = "application/ld+json";
    script.text = JSON.stringify(jsonLd);
    this.document.head.appendChild(script);
  }
}
