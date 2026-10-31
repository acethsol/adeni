import { routes } from "./app.routes";

describe("business app routes", () => {
  it("exposes public /welcome without auth guards on the route", () => {
    const welcome = routes.find((route) => route.path === "welcome");
    expect(welcome).toBeDefined();
    expect(welcome?.canActivate).toBeUndefined();
    expect(welcome?.loadComponent).toBeDefined();
  });

  it("redirects legacy /setup to welcome", () => {
    const setup = routes.find((route) => route.path === "setup");
    expect(setup?.redirectTo).toBe("welcome");
  });
});
