import { routes } from "../app.routes";
import { portalPermissionGuard } from "./guards/portal-permission.guard";

describe("customer detail route compatibility", () => {
  it("keeps the existing URL, title and permission guard while deferring its feature code", () => {
    const shell = routes.find(route => route.children?.some(child => child.path === "customers/:id"));
    const detail = shell?.children?.find(route => route.path === "customers/:id");
    expect(detail?.title).toBe("Customer"); expect(detail?.canActivate).toContain(portalPermissionGuard);
    expect(detail?.loadComponent).toBeDefined(); expect(detail?.component).toBeUndefined();
  });
});
