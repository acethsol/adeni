import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  transpilePackages: ["@adeni/api-client", "@adeni/shared"],
  images: {
    remotePatterns: [
      {
        protocol: "https",
        hostname: "images.unsplash.com",
      },
      {
        protocol: "http",
        hostname: "localhost",
        port: "5169",
        pathname: "/media/**",
      },
      {
        protocol: "http",
        hostname: "127.0.0.1",
        port: "5169",
        pathname: "/media/**",
      },
    ],
  },
};

export default nextConfig;
