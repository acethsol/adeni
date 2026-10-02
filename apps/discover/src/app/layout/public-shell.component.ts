import { Component } from "@angular/core";
import { RouterLink, RouterLinkActive, RouterOutlet } from "@angular/router";

@Component({
  selector: "app-public-shell",
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: "./public-shell.component.html",
  styleUrl: "./public-shell.component.scss",
})
export class PublicShellComponent {}
