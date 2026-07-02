export function isAuth0Configured(): boolean {
  return Boolean(
    process.env.AUTH0_DOMAIN &&
      process.env.AUTH0_CLIENT_ID &&
      process.env.AUTH0_CLIENT_SECRET &&
      process.env.AUTH0_SECRET &&
      process.env.APP_BASE_URL,
  );
}

export function getAuth0Audience(): string {
  return process.env.AUTH0_AUDIENCE ?? "https://api.adeni.io";
}
