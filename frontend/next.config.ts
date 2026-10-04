import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  output: "standalone",
  async rewrites() {
    return [
      {
        source: '/api/back/:path*',
        destination: 'http://localhost:9000/:path*', // Адрес вашего реального бэкенда
      },
    ];
  },
};

export default nextConfig;
