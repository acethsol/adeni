import { Component } from "@angular/core";
import { RouterOutlet } from "@angular/router";
import {
  AdeniConfirmHostComponent,
  AdeniGlobalLoadingPanelComponent,
  AdeniToastHostComponent,
} from "@adeni/ui";

@Component({
  imports: [
    RouterOutlet,
    AdeniGlobalLoadingPanelComponent,
    AdeniToastHostComponent,
    AdeniConfirmHostComponent,
  ],
  selector: "app-root",
  templateUrl: "./app.html",
})
export class App {}
