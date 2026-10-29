import { Injectable } from "@angular/core";
import { Subject } from "rxjs";

/** Coordinates topbar / sidebar badge refresh after messaging read, etc. */
@Injectable({ providedIn: "root" })
export class BusinessNavBadgesService {
  private readonly refreshRequested = new Subject<void>();
  readonly refreshRequested$ = this.refreshRequested.asObservable();

  requestRefresh(): void {
    this.refreshRequested.next();
  }
}
