import { createApp } from "./lib/app.js";

const port = Number(process.env.PORT || 1729);
const host = process.env.HOST || "0.0.0.0";

const app = await createApp();
app.listen(port, host, () => {
  console.log(`PVZ Server слушает http://${host}:${port}`);
});
