import { createApp } from "../lib/app.js";

let appPromise = null;

async function expressApp() {
  if (!appPromise) {
    appPromise = createApp().catch((error) => {
      appPromise = null;
      throw error;
    });
  }
  return appPromise;
}

export default async function handler(request, response) {
  const app = await expressApp();
  return app(request, response);
}
