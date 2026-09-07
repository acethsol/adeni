import appJson from "./app.json";

const allowCleartext = process.env.EXPO_PUBLIC_ALLOW_CLEARTEXT === "true";

export default ({ config }: { config: typeof appJson.expo }) => ({
  ...config,
  android: {
    ...config.android,
    usesCleartextTraffic: allowCleartext,
  },
});
