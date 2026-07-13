import { Compass } from "lucide-react";
import { Button } from "@/components/ui/button";

export default function NotFound() {
  return (
    <div id="main-content" className="flex min-h-screen flex-col items-center justify-center bg-background px-6 text-center">
      <div className="flex h-14 w-14 items-center justify-center rounded-full bg-accent/10 text-accent">
        <Compass className="h-7 w-7" aria-hidden />
      </div>
      <p className="mt-6 text-sm font-semibold uppercase tracking-widest text-accent">404</p>
      <h1 className="mt-2 text-2xl font-bold text-foreground">Page not found</h1>
      <p className="mt-3 max-w-md text-sm leading-relaxed text-muted">
        The page you&apos;re looking for doesn&apos;t exist or may have moved. Let&apos;s get you
        back on track.
      </p>
      <div className="mt-8 flex flex-wrap justify-center gap-3">
        <Button variant="secondary" href="/discover">
          Browse businesses
        </Button>
        <Button href="/">Go home</Button>
      </div>
    </div>
  );
}
