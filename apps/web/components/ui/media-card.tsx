import type { ReactNode } from "react";
import { Card } from "@/components/ui/card";
import { RemoteImage } from "@/components/ui/remote-image";
import { cn } from "@/lib/cn";

type MediaCardProps = {
  children: ReactNode;
  className?: string;
  interactive?: boolean;
};

export function MediaCard({ children, className, interactive = true }: MediaCardProps) {
  return (
    <Card
      padding="none"
      interactive={interactive}
      className={cn("flex h-full flex-col overflow-hidden", className)}
    >
      {children}
    </Card>
  );
}

type MediaCardImageProps = {
  src: string;
  fallbackSrc: string;
  alt?: string;
  sizes?: string;
  className?: string;
};

export function MediaCardImage({
  src,
  fallbackSrc,
  alt = "",
  sizes = "280px",
  className,
}: MediaCardImageProps) {
  return (
    <div className={cn("relative aspect-[5/4] w-full overflow-hidden bg-muted", className)}>
      <RemoteImage
        src={src}
        fallbackSrc={fallbackSrc}
        alt={alt}
        fill
        sizes={sizes}
        className="object-cover"
      />
    </div>
  );
}

export function MediaCardBody({ children, className }: { children: ReactNode; className?: string }) {
  return <div className={cn("flex flex-1 flex-col gap-1.5 p-4", className)}>{children}</div>;
}

export function MediaCardTitle({ children, className }: { children: ReactNode; className?: string }) {
  return (
    <h3 className={cn("truncate text-sm font-semibold leading-snug text-foreground", className)}>
      {children}
    </h3>
  );
}

export function MediaCardMeta({ children, className }: { children: ReactNode; className?: string }) {
  return <p className={cn("truncate text-sm text-muted", className)}>{children}</p>;
}

export function MediaCardActions({ children, className }: { children: ReactNode; className?: string }) {
  return <div className={cn("mt-auto flex flex-wrap gap-2 pt-2", className)}>{children}</div>;
}
