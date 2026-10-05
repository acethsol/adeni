import { Component } from "@angular/core";
import { RouterLink } from "@angular/router";

@Component({
  selector: "app-setup",
  standalone: true,
  imports: [RouterLink],
  templateUrl: "./setup.component.html",
  styleUrl: "./setup.component.scss",
})
export class SetupComponent {}
