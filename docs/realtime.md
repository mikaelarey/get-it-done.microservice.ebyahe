# Live record updates

The authenticated SignalR endpoint is `/api/live`, with the same Keycloak validation as eByahe REST and token-subject user IDs. There are no client-controlled subscriptions. Browser query tokens work only on this exact hub path. Expiring tokens close connections; reconnection obtains a fresh token. Explicit frontend CORS and WebSocket Origin allowlists are required.

After committing a successful write, repositories publish `RecordsChanged` containing only resource names:

| Write | Resources |
| --- | --- |
| Bus creation | buses |
| Terminal creation | terminals |
| Scheduled trip creation | scheduled-trips |
| Booking submission and seat reservation | bookings, scheduled-trips |
| Partner application submission | applications |
| Review/approve/decline | applications, partners |
| Member request and Kafka provisioning completion | users |

Delivery targets the affected authenticated owner and current active members of that approved partner, resolved from the database. Application reviewers require both web-client `admin` and API-client `partner.review`; booking readers require web-client `admin` plus API-client `booking.read`. Reviewers also receive record invalidations for partner summary tiles. No record contents, credentials or passwords travel through the hub. All follow-up REST calls retain their existing authorization checks.

The eByahe microfrontend shares one connection across its active pages in any shell or standalone mode. Lists refresh only for their resource, identity, filters, pagination, explicit refresh, or connection catch-up. Profile-object replacement during routine session renewal no longer reloads lists or resets forms. User provisioning waits for a live `users` update rather than checking every three seconds. The Add User dialog still provides manual Check status.

For hosting, forward the existing API prefix including `/api/live` and `/api/live/negotiate`; enable WebSocket upgrades, disable SSE buffering and redact `access_token` in proxy request logs. Clients use WebSockets with SSE fallback and reconnect backoff, without long polling. Existing HTTP loads on initial entry and reconnection recover missed updates. Broadcast failure is logged without changing a committed business result.

The default lifetime manager supports one API process. Multi-replica hosting needs a shared Redis backplane or Azure SignalR and the appropriate affinity before scale-out; direct repository invalidations do not cross processes by themselves. No schema migration or Kafka topic change is needed.
