/// <reference types="vite/client" />

interface ImportMetaEnv {
  /**
   * The gateway's origin. Optional: the default in src/app/config.ts is the local gateway port, so
   * a fresh clone runs without an .env file (quickstart.md).
   */
  readonly VITE_GATEWAY_ORIGIN?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
