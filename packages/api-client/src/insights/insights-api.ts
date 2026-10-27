import type { ApiClientContext } from "../core/api-transport";
import type { InsightsOperations } from "./insights-operations";
/** Keep optional customer analytics and private PDF validation out of startup bundles. */
export class InsightsApi {
  private loaded?: Promise<InsightsOperations>;
  constructor(private readonly client: ApiClientContext) {}
  private operations(): Promise<InsightsOperations> { return this.loaded ??= import("./insights-operations").then(m => new m.InsightsOperations(this.client)); }
  segments(...args: Parameters<InsightsOperations["segments"]>): ReturnType<InsightsOperations["segments"]> { return this.operations().then(api => api.segments(...args)); }
  batch(...args: Parameters<InsightsOperations["batch"]>): ReturnType<InsightsOperations["batch"]> { return this.operations().then(api => api.batch(...args)); }
  definitions(...args: Parameters<InsightsOperations["definitions"]>): ReturnType<InsightsOperations["definitions"]> { return this.operations().then(api => api.definitions(...args)); }
  preview(...args: Parameters<InsightsOperations["preview"]>): ReturnType<InsightsOperations["preview"]> { return this.operations().then(api => api.preview(...args)); }
}
