import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  transpilePackages: ["@adeni/api-client", "@adeni/shared"],
};

export default nextConfig;
