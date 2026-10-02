import { inject, Injectable } from "@angular/core";
import { AdeniApiClient } from "@adeni/api-client";
import { ADENI_DISCOVER_CONFIG } from "../adeni-config";

@Injectable({ providedIn: "root" })
export class DiscoverApiService {
  private readonly config = inject(ADENI_DISCOVER_CONFIG);

  createClient(): AdeniApiClient {
    return new AdeniApiClient({ baseUrl: this.config.apiBaseUrl });
  }
}
