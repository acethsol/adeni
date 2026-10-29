import { routes } from "../app.routes";
import { businessPermissionGuard } from "./guards/business-permission.guard";

describe("customer detail route compatibility", () => {
  it("keeps the existing URL, title and permission guard while deferring its feature code", () => {
    const shell = routes.find(route => route.children?.some(child => child.path === "customers/:id"));
    const detail = shell?.children?.find(route => route.path === "customers/:id");
    expect(detail?.title).toBe("Customer"); expect(detail?.canActivate).toContain(businessPermissionGuard);
    expect(detail?.loadComponent).toBeDefined(); expect(detail?.component).toBeUndefined();
  });
});
