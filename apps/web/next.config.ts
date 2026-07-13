import type { NextConfig } from "next";

const apiBaseUrl = process.env.ADENI_API_URL ?? "http://localhost:5169";

const nextConfig: NextConfig = {
  transpilePackages: ["@adeni/api-client", "@adeni/shared"],
  async rewrites() {
    return [
      {
        source: "/api/v1/:path*",
        destination: `${apiBaseUrl}/api/v1/:path*`,
      },
    ];
  },
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
