import { setupServer } from 'msw/node';

/**
 * The shared request-mocking server for component tests. Handlers are registered per test with
 * `server.use(...)` rather than collected in a global list: a story's tests should state the
 * backend responses they depend on, so a reader can see what the component was given without
 * chasing a shared fixture file.
 */
export const server = setupServer();
