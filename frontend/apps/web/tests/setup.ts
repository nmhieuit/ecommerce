import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterAll, afterEach, beforeAll } from 'vitest';
import { server } from './msw/server';

// `error` rather than `warn` on an unhandled request: a component that calls something no test
// stubbed is a test that is silently exercising the network, and spec SC-010 is precisely about
// knowing where requests go.
beforeAll(() => server.listen({ onUnhandledRequest: 'error' }));

afterEach(() => {
  cleanup();
  server.resetHandlers();
});

afterAll(() => server.close());
