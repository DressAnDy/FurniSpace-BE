# FurniSpace SignalR Notification Guide

This guide explains how realtime notifications and UI refresh events work in the FurniSpace backend using SignalR.

Related Jira: **SCRUM-94 (NOTI-07)** — SignalR Notification Hub.

## 1. Current Status

Implemented (SCRUM-94):

- Hub endpoint: `/hubs/notifications`
- JWT authentication for hub connections
- Automatic group join on connect:
  - `user:{accountId}`
  - `role:{ROLE}` for each role claim in the access token
- Token sources: `access_token` cookie **or** `?access_token=` query string

Not implemented yet (follow-up stories):

- **SCRUM-95 (NOTI-08):** `IRealtimeNotificationService` wrapper used by business modules
- **SCRUM-58 (NOTI-01):** `NotificationDispatcher` routing by delivery level
- **SCRUM-98 (NOTI-09):** SMTP for `EMAIL_IN_APP_REALTIME` events
- Redis SignalR backplane for multi-instance scale-out

Business modules must **not** call `IHubContext` directly once SCRUM-95 is done. They should use `IRealtimeNotificationService` instead.

## 2. Architecture

```text
Client (Web / Mobile)
  -> WebSocket negotiate: GET /hubs/notifications/negotiate
  -> JWT validated (cookie or query string)
  -> NotificationsHub.OnConnectedAsync
       -> join user:{accountId}
       -> join role:{ROLE} for each JWT role claim

(Future) Project/File/Notification services
  -> NotificationDispatcher
  -> IRealtimeNotificationService
  -> SignalR groups
  -> Client receives event
```

FurniSpace does not use CQRS. SignalR is infrastructure realtime transport; workflow rules stay in Application services.

## 3. Hub Endpoint

| Item | Value |
|---|---|
| Hub path | `/hubs/notifications` |
| Hub class | `FurniSpace.API.Hubs.NotificationsHub` |
| Auth | Required (`[Authorize]`) |
| Transport | WebSockets (SignalR default) |

Negotiate URL example:

```text
https://api.example.com/hubs/notifications/negotiate?negotiateVersion=1
```

The story text `GET /hubs/notifications` refers to the hub route prefix. SignalR clients call `negotiate` automatically; do not treat it as a normal REST GET API.

## 4. Authentication

The API already uses JWT Bearer auth. SignalR reuses the same validation:

| Source | When to use |
|---|---|
| `access_token` HTTP-only cookie | Same-site web app (already supported for REST) |
| `?access_token={jwt}` query string | SPA/mobile WebSocket clients that cannot rely on cookies |

Query-string token is read only for paths under `/hubs/*`.

Access tokens are short-lived (default 15 minutes). Clients should refresh the session and reconnect when the token expires.

Revoked tokens (logout / rotation) are rejected the same way as REST APIs via `IAuthService.IsAccessTokenRevokedAsync`.

## 5. Connection Groups

On successful connect, the server adds the connection to groups defined in `RealtimeGroupNames`:

| Group pattern | Example | Purpose |
|---|---|---|
| `user:{accountId}` | `user:3fa85f64-5717-4562-b3fc-2c963f66afa6` | Direct user notifications |
| `role:{ROLE}` | `role:ADMIN` | Broadcast to all connections with that role |

### Role names (important)

Groups use **JWT role claims**, which match FurniSpace seed data and `[Authorize(Roles = "...")]`:

- `ADMIN`
- `SALES`
- `DESIGNER`
- `CUSTOMER`

Do **not** use Jira doc aliases such as `SALES_CONSULTANT` or `DESIGNER_STAFF` unless the JWT and database roles are renamed consistently.

Role group names are normalized to uppercase in `RealtimeGroupNames.Role`.

## 6. Client Connection Examples

### JavaScript (`@microsoft/signalr`)

```typescript
import * as signalR from "@microsoft/signalr";

const accessToken = "..."; // from login API

const connection = new signalR.HubConnectionBuilder()
  .withUrl("https://api.example.com/hubs/notifications", {
    accessTokenFactory: () => accessToken,
  })
  .withAutomaticReconnect()
  .build();

connection.on("project.status.changed", (payload) => {
  console.log("Status changed", payload);
});

connection.on("notification.created", (payload) => {
  console.log("New notification", payload);
});

await connection.start();
```

### Cookie-based web client (same origin)

If the browser already has `access_token` cookie from login, `withUrl("/hubs/notifications")` may work without `accessTokenFactory` when credentials are included and CORS allows credentials.

## 7. Event Names (planned)

These event names come from the notification backlog. The hub does not define C# hub methods for them; the server will **push** them via `IHubContext` / `IRealtimeNotificationService` in later stories.

| Event | Delivery level | Typical receiver group |
|---|---|---|
| `project.request.submitted` | `IN_APP_REALTIME` | `role:SALES`, `role:ADMIN` |
| `project.request.accepted` | `EMAIL_IN_APP_REALTIME` | `user:{customerId}` |
| `project.more_information.requested` | `EMAIL_IN_APP_REALTIME` | `user:{customerId}` |
| `project.basic_information.updated` | `IN_APP_REALTIME` | `user:{assignedSalesId}` |
| `project.status.changed` | `REALTIME_ONLY` | project participants (future) |
| `project.request.rejected` | `EMAIL_IN_APP_REALTIME` | `user:{customerId}` |
| `project.designer.assigned` | `IN_APP_REALTIME` | `user:{designerId}` |
| `project.file.uploaded` | `IN_APP_REALTIME` or `REALTIME_ONLY` | depends on project status |
| `notification.created` | `IN_APP_REALTIME` / `EMAIL_IN_APP_REALTIME` | `user:{receiverId}` |
| `project.file_link.deleted` | `REALTIME_ONLY` | project UI subscribers |
| `project.file.archived` | `REALTIME_ONLY` | project UI subscribers |

Payload shape is JSON per event; exact DTOs will be defined when `NotificationDispatcher` is implemented (SCRUM-58).

## 8. Delivery Levels (policy)

| Level | DB notification row | SignalR | SMTP |
|---|---|---|---|
| `REALTIME_ONLY` | No | Yes | No |
| `IN_APP_REALTIME` | Yes | Yes (`notification.created`) | No |
| `EMAIL_IN_APP_REALTIME` | Yes | Yes | Yes |

SignalR/SMTP failure must **not** rollback the main business transaction in MVP. Failures should be logged.

## 9. CORS and WebSockets

`Program.cs` enables CORS with `AllowCredentials()`. For browser clients:

- Prefer explicit origins in production (`CORS_ALLOWED_ORIGINS`), not `*`
- WebSocket connections require the frontend origin to be allowed
- Use `accessTokenFactory` or cookie auth consistently with your deployment topology

## 10. Local Development

1. Start API (`dotnet run` in `FurniSpace.API`)
2. Login via auth API to obtain access token
3. Connect a SignalR client to `https://localhost:{port}/hubs/notifications`
4. Verify negotiate returns `200` with valid token; `401` without token

No extra Docker service is required for SignalR in MVP (in-memory backplane).

## 11. Recommended Implementation Order

1. **SCRUM-94** — Hub + groups (this guide)
2. **SCRUM-95** — `IRealtimeNotificationService`
3. **SCRUM-58** — `NotificationDispatcher` + templates
4. **SCRUM-98** — SMTP sender
5. **SCRUM-100+** — Project/File update stories calling dispatcher

## 12. Code References

| File | Responsibility |
|---|---|
| `src/FurniSpace.API/Hubs/NotificationsHub.cs` | Hub + `OnConnectedAsync` group join |
| `src/FurniSpace.Application/Common/Realtime/RealtimeGroupNames.cs` | Group name helpers + hub path constant |
| `src/FurniSpace.API/Program.cs` | `AddSignalR`, `MapHub`, JWT query token for `/hubs` |

## 13. Known Limitations (MVP)

- Single API instance only (no Redis backplane)
- No hub methods exposed to clients (server-push only)
- No `SendToProjectUsersAsync` until SCRUM-95
- Project-scoped groups (e.g. `project:{projectId}`) are not joined on connect; add when project chat/realtime UI needs them
