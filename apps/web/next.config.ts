import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  transpilePackages: ["@adeni/api-client", "@adeni/shared"],
  images: {
    remotePatterns: [
      {
        protocol: "https",
        hostname: "images.unsplash.com",
      },
    ],
  },
};

export default nextConfig;
