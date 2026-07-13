"use client";

import Image, { type ImageProps } from "next/image";
import { useState } from "react";

type Props = Omit<ImageProps, "src" | "onError"> & {
  src: string;
  fallbackSrc: string;
  priority?: boolean;
};

export function RemoteImage({ src, fallbackSrc, alt = "", ...rest }: Props) {
  const [currentSrc, setCurrentSrc] = useState(src);

  return (
    <Image
      {...rest}
      alt={alt}
      src={currentSrc}
      onError={() => {
        if (currentSrc !== fallbackSrc) {
          setCurrentSrc(fallbackSrc);
        }
      }}
    />
  );
}
