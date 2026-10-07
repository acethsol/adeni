import {
  afterNextRender,
  Component,
  effect,
  ElementRef,
  inject,
  input,
  OnDestroy,
  output,
  signal,
  viewChild,
} from "@angular/core";
import type { DiscoveryBusinessItem } from "@adeni/shared";
import {
  getCategoryLabel,
  resolveBusinessImageUrls,
} from "@adeni/shared";
import { AdeniLocaleService } from "@adeni/ui";
import { ADENI_DISCOVER_CONFIG } from "../core/adeni-config";

type MapboxNS = typeof import("mapbox-gl");
type MapboxMap = import("mapbox-gl").Map;
type MapboxMarker = import("mapbox-gl").Marker;
type MapboxPopup = import("mapbox-gl").Popup;

@Component({
  selector: "app-discovery-map",
  standalone: true,
  template: `
    <div class="map-shell" [class.ready]="ready()" [class.failed]="failed()">
      <div #mapHost class="map-host" role="presentation"></div>
      @if (!ready() && !failed()) {
        <div class="map-fallback" aria-hidden="true">
          <span></span>
        </div>
      }
      @if (failed()) {
        <div class="map-error" role="status">
          <p>{{ errorMessage() }}</p>
        </div>
      }
    </div>
  `,
  styleUrl: "./discovery-map.component.scss",
})
export class DiscoveryMapComponent implements OnDestroy {
  private readonly config = inject(ADENI_DISCOVER_CONFIG);
  private readonly localeService = inject(AdeniLocaleService);

  readonly items = input.required<DiscoveryBusinessItem[]>();
  readonly activeLocationId = input<string | null>(null);
  readonly center = input<{ lat: number; lng: number }>({ lat: 6.5244, lng: 3.3792 });
  readonly markerSelect = output<string>();

  private readonly mapHost = viewChild.required<ElementRef<HTMLDivElement>>("mapHost");
  readonly ready = signal(false);
  readonly failed = signal(false);
  readonly errorMessage = signal("Map couldn’t load. Check your connection and refresh.");

  private mapbox: MapboxNS | null = null;
  private map: MapboxMap | null = null;
  private markers = new Map<string, MapboxMarker>();
  private popup: MapboxPopup | null = null;
  private resizeObserver: ResizeObserver | null = null;
  private sizeTimers: number[] = [];
  private fittedKey = "";
  private previewLocationId: string | null = null;

  constructor() {
    afterNextRender(() => {
      void this.initMap();
    });

    effect(() => {
      const items = this.items();
      const activeId = this.activeLocationId();
      const center = this.center();
      if (!this.ready() || !this.map || !this.mapbox) {
        return;
      }
      this.syncMarkers(items, activeId, center);
    });
  }

  ngOnDestroy(): void {
    this.resizeObserver?.disconnect();
    this.resizeObserver = null;
    for (const id of this.sizeTimers) {
      window.clearTimeout(id);
    }
    this.sizeTimers = [];
    this.closePreview();
    for (const marker of this.markers.values()) {
      marker.remove();
    }
    this.markers.clear();
    this.map?.remove();
    this.map = null;
    this.mapbox = null;
  }

  private async initMap(): Promise<void> {
    const token = this.config.mapboxAccessToken.trim();
    if (!token) {
      this.errorMessage.set(
        "Add a Mapbox public token (mapboxAccessToken) in the discover environment to show the map.",
      );
      this.failed.set(true);
      return;
    }

    try {
      const host = this.mapHost().nativeElement;
      await new Promise<void>((resolve) => requestAnimationFrame(() => resolve()));

      const mod = await import("mapbox-gl");
      const mapboxgl = (mod as MapboxNS & { default?: MapboxNS }).default ?? mod;
      this.mapbox = mapboxgl;
      mapboxgl.accessToken = token;

      const center = this.center();
      this.map = new mapboxgl.Map({
        container: host,
        style: "mapbox://styles/mapbox/streets-v12",
        center: [center.lng, center.lat],
        zoom: 11.5,
        attributionControl: false,
        logoPosition: "bottom-left",
        scrollZoom: false,
        cooperativeGestures: true,
      });

      this.map.addControl(
        new mapboxgl.AttributionControl({ compact: true }),
        "bottom-right",
      );
      this.map.addControl(new mapboxgl.NavigationControl({ showCompass: false }), "top-right");

      this.map.on("click", () => this.closePreview());

      await new Promise<void>((resolve, reject) => {
        this.map!.once("load", () => resolve());
        this.map!.once("error", (event) => reject(event.error ?? event));
      });

      this.resizeObserver = new ResizeObserver(() => this.map?.resize());
      this.resizeObserver.observe(host);
      if (host.parentElement) {
        this.resizeObserver.observe(host.parentElement);
      }

      this.syncMarkers(this.items(), this.activeLocationId(), center);
      this.ready.set(true);
      this.bumpSize();
    } catch (reason) {
      console.error("Discovery map failed to initialize", reason);
      this.errorMessage.set("Map couldn’t load. Check the Mapbox token and refresh.");
      this.failed.set(true);
    }
  }

  private bumpSize(): void {
    for (const delay of [0, 50, 150, 350, 800]) {
      const id = window.setTimeout(() => this.map?.resize(), delay);
      this.sizeTimers.push(id);
    }
  }

  private syncMarkers(
    items: DiscoveryBusinessItem[],
    activeId: string | null,
    center: { lat: number; lng: number },
  ): void {
    if (!this.map || !this.mapbox) {
      return;
    }

    const nextIds = new Set(items.map((item) => item.locationId));
    for (const [id, marker] of this.markers) {
      if (!nextIds.has(id)) {
        marker.remove();
        this.markers.delete(id);
      }
    }

    const bounds = new this.mapbox.LngLatBounds();
    let hasBounds = false;

    for (const item of items) {
      const lat = item.latitude;
      const lng = item.longitude;
      if (lat == null || lng == null || Number.isNaN(lat) || Number.isNaN(lng)) {
        continue;
      }
      bounds.extend([lng, lat]);
      hasBounds = true;

      let marker = this.markers.get(item.locationId);
      const active = item.locationId === activeId;
      if (!marker) {
        const el = document.createElement("button");
        el.type = "button";
        el.className = `adeni-pin${active ? " active" : ""}`;
        el.setAttribute("aria-label", item.name);
        el.addEventListener("click", (event) => {
          event.stopPropagation();
          this.markerSelect.emit(item.locationId);
          this.showPreview(item);
        });
        marker = new this.mapbox.Marker({ element: el, anchor: "center" })
          .setLngLat([lng, lat])
          .addTo(this.map);
        this.markers.set(item.locationId, marker);
      } else {
        marker.setLngLat([lng, lat]);
        marker.getElement().classList.toggle("active", active);
      }
    }

    if (this.previewLocationId && !nextIds.has(this.previewLocationId)) {
      this.closePreview();
    }

    const key = `${items.length}:${items[0]?.locationId ?? ""}:${center.lat}:${center.lng}`;
    if (key !== this.fittedKey) {
      this.fittedKey = key;
      if (hasBounds && items.length === 1) {
        this.map.jumpTo({ center: bounds.getCenter(), zoom: 13 });
      } else if (hasBounds) {
        this.map.fitBounds(bounds, { padding: 56, maxZoom: 13, duration: 0 });
      } else {
        this.map.jumpTo({ center: [center.lng, center.lat], zoom: 12 });
      }
      this.bumpSize();
    } else {
      this.map.resize();
    }
  }

  private showPreview(item: DiscoveryBusinessItem): void {
    if (!this.map || !this.mapbox || item.latitude == null || item.longitude == null) {
      return;
    }

    this.closePreview();
    this.previewLocationId = item.locationId;

    const images = resolveBusinessImageUrls(item.categorySlug, item.coverImageUrl, item.imageUrls);
    const category = getCategoryLabel(this.localeService.locale(), item.categorySlug);
    const rating =
      item.ratingAvg != null && (item.reviewCount ?? 0) > 0
        ? `★ ${item.ratingAvg.toFixed(1)}`
        : "";
    const meta = [category, item.area, item.distanceKm != null ? `${item.distanceKm.toFixed(1)} km` : ""]
      .filter(Boolean)
      .join(" · ");
    const multi = images.length > 1;

    const root = document.createElement("div");
    root.className = "adeni-preview";
    root.innerHTML = `
      <div class="adeni-preview-media${multi ? " has-carousel" : ""}">
        <a class="adeni-preview-link" href="/businesses/${encodeURIComponent(item.slug)}">
          <img class="adeni-preview-img" src="${escapeHtml(images[0] ?? "")}" alt="" data-preview-img />
        </a>
        ${
          multi
            ? `
          <button type="button" class="adeni-preview-nav prev" aria-label="Previous photo" data-prev>‹</button>
          <button type="button" class="adeni-preview-nav next" aria-label="Next photo" data-next>›</button>
          <div class="adeni-preview-dots" aria-hidden="true" data-dots>
            ${images.map((_, i) => `<span class="dot${i === 0 ? " on" : ""}"></span>`).join("")}
          </div>
        `
            : ""
        }
      </div>
      <a class="adeni-preview-link body-link" href="/businesses/${encodeURIComponent(item.slug)}">
        <div class="adeni-preview-body">
          <div class="adeni-preview-title-row">
            <strong>${escapeHtml(item.name)}</strong>
            ${rating ? `<span class="adeni-preview-rating">${rating}</span>` : ""}
          </div>
          <p>${escapeHtml(meta)}</p>
        </div>
      </a>
    `;

    if (multi) {
      let index = 0;
      const img = root.querySelector<HTMLImageElement>("[data-preview-img]");
      const dots = root.querySelectorAll<HTMLElement>("[data-dots] .dot");
      const setIndex = (next: number) => {
        index = (next + images.length) % images.length;
        if (img) img.src = images[index] ?? "";
        dots.forEach((dot, i) => dot.classList.toggle("on", i === index));
      };
      root.querySelector("[data-prev]")?.addEventListener("click", (event) => {
        event.preventDefault();
        event.stopPropagation();
        setIndex(index - 1);
      });
      root.querySelector("[data-next]")?.addEventListener("click", (event) => {
        event.preventDefault();
        event.stopPropagation();
        setIndex(index + 1);
      });
    }

    this.popup = new this.mapbox.Popup({
      closeButton: true,
      closeOnClick: false,
      offset: 18,
      maxWidth: "340px",
      className: "adeni-map-popup",
      anchor: "bottom",
    })
      .setLngLat([item.longitude, item.latitude])
      .setDOMContent(root)
      .addTo(this.map);

    this.popup.on("close", () => {
      this.previewLocationId = null;
      this.popup = null;
    });
  }

  private closePreview(): void {
    this.popup?.remove();
    this.popup = null;
    this.previewLocationId = null;
  }
}

function escapeHtml(value: string): string {
  return value
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#39;");
}
