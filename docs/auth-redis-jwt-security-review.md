# Authentication Redis JWT Security Review

This document records the current security review notes for the FurniSpace authentication flow that uses JWT access tokens, Redis-backed refresh token storage, and Redis-backed access token revocation.

## Scope

Reviewed areas:

- JWT bearer validation in `src/FurniSpace.API/Program.cs`
- Token creation in `src/FurniSpace.Application/Services/Identity/JwtTokenService.cs`
- Refresh token and access token revocation storage in `src/FurniSpace.Application/Services/Identity/RefreshTokenStore.cs`
- Auth Redis key generation in `src/FurniSpace.Application/Services/Identity/RefreshTokenStore.cs`
- Redis cache implementation in `src/FurniSpace.Infrastructure/Caching/RedisCacheService.cs`
- Redis container configuration in `docker-compose.yml`

## Summary

The current design has several good foundations:

- Access tokens are signed with HS256 and validated with issuer, audience, lifetime, and signing key checks.
- Access token revocation uses the JWT `jti` and stores blacklist entries with a TTL matching the remaining token lifetime.
- Refresh tokens are generated with cryptographically secure random bytes.
- Refresh token Redis keys use a SHA-256 hash of the token instead of storing the raw token in the key.
- Redis is not exposed directly to the host in `docker-compose.yml`.

The original main security risks were weak JWT secret enforcement, Redis eviction policy for auth data, accepting tokens without `jti`, and missing refresh token rotation semantics. Most code-level risks have now been remediated; endpoint-level refresh rotation and deeper integration tests remain.

## Findings

| Severity | Area | Issue |
| --- | --- | --- |
| High | JWT secret | Remediated: startup and token signing reject secrets shorter than 32 bytes. |
| High | Redis auth state | Remediated for current single Redis instance: Docker Compose now uses `noeviction`. |
| Medium | JWT revocation | Remediated: tokens without `jti` are rejected. |
| Medium | Refresh tokens | Partially remediated: service-level rotation exists; refresh endpoint still needs to call it. |
| Low | JWT bearer options | Remediated: `SaveToken` is disabled. |

## Required Changes

### 1. Enforce a strong JWT secret

Files:

- `src/FurniSpace.API/Program.cs`
- `src/FurniSpace.Application/Services/Identity/JwtTokenService.cs`
- `src/FurniSpace.Application/Common/Auth/JwtSettings.cs`

Current risk:

The startup code only rejects an empty secret. A short or predictable value such as `secret`, `abc123`, or a shared development value would still be accepted. Because HS256 uses a symmetric key, a weak key can allow offline brute-force attacks against captured tokens.

Recommended change:

- Require at least 32 bytes of entropy for the signing key.
- Prefer storing `JWT_SECRET` as a base64-encoded 256-bit or stronger random value.
- Fail startup if the configured secret is too short.
- Keep production secrets only in environment variables, secret managers, or deployment configuration.

Suggested validation rule:

```text
JWT secret must be at least 32 bytes after decoding or UTF-8 conversion.
```

### 2. Do not use LRU eviction for authentication Redis data

File:

- `docker-compose.yml`

Current configuration:

```yaml
command: ["redis-server", "--requirepass", "${REDIS_PASSWORD}", "--maxmemory", "256mb", "--maxmemory-policy", "allkeys-lru"]
```

Current risk:

`allkeys-lru` allows Redis to evict any key, including:

- Active refresh token/session keys
- Access token blacklist keys
- OTP or password reset keys if added later

If an access token blacklist key is evicted before the JWT expires, a revoked token may become valid again until its natural expiration.

Recommended change:

- Use `noeviction` for Redis instances that store authentication state.
- Alternatively, split Redis into separate instances: one for normal cache data with eviction, and one for auth/session/security state without eviction.

Recommended local compose value:

```yaml
command: ["redis-server", "--requirepass", "${REDIS_PASSWORD}", "--maxmemory", "256mb", "--maxmemory-policy", "noeviction"]
```

### 3. Reject JWT access tokens that do not contain `jti`

File:

- `src/FurniSpace.API/Program.cs`

Current behavior:

`OnTokenValidated` checks the blacklist only when a `jti` claim exists. If the claim is missing, validation continues.

Current risk:

The logout/revocation model depends on `jti`. A token without `jti` cannot be checked against the Redis blacklist, so accepting it weakens the revocation guarantee.

Recommended change:

- Fail validation when `jti` is missing or blank.
- Continue checking Redis blacklist when `jti` is present.

Expected behavior:

```text
Valid JWT signature + issuer + audience + lifetime + required jti + not blacklisted.
```

### 4. Implement refresh token rotation

Files:

- `src/FurniSpace.Application/Services/Identity/AuthService.cs`
- `src/FurniSpace.Application/Services/Identity/RefreshTokenStore.cs`
- `src/FurniSpace.Application/Interfaces/Identity/IAuthService.cs`
- Future refresh endpoint/controller code

Current behavior:

The service can create, validate, and revoke refresh tokens, but there is no enforced token rotation flow in the service yet.

Current risk:

If a refresh token leaks, it can remain valid until expiration unless the user logs out or the token is explicitly revoked.

Recommended change:

- On refresh, validate the current refresh token.
- Revoke the current refresh token immediately after successful validation.
- Issue a new access token and a new refresh token.
- Store the new refresh token in Redis with its own TTL.
- If a revoked or missing refresh token is used, treat it as a possible replay event and revoke the user's active sessions if the product requires stronger session security.

Recommended service-level API:

```csharp
Task<AuthResponseDto?> RotateRefreshTokenAsync(
    Guid userId,
    string refreshToken,
    string email,
    string fullName,
    IEnumerable<string>? roles = null,
    CancellationToken cancellationToken = default);
```

### 5. Disable `SaveToken` unless raw token access is required

File:

- `src/FurniSpace.API/Program.cs`

Current behavior:

```csharp
options.SaveToken = true;
```

Current risk:

This is not a direct vulnerability, but storing the raw bearer token in authentication properties increases exposure if later code logs or reads authentication properties incorrectly.

Recommended change:

Set it to `false` or remove the line unless the application explicitly needs to access the original bearer token after validation.

## Additional Hardening Recommendations

### Add login rate limiting

Files to extend:

- `src/FurniSpace.Infrastructure/Caching/RedisCacheService.cs`
- Login endpoint/service when implemented

Redis already has `IncrementAsync`, which can support login attempt counters. Use atomic increment with short TTL windows to reduce brute-force risk.

Recommended key pattern:

```text
furnispace:auth:login-attempt:{normalizedEmailOrIp}
```

### Avoid storing sensitive values in Redis values

Current refresh token keys are hashed, which is good. Continue avoiding raw storage of:

- Passwords
- Raw refresh tokens
- Raw OTP values
- Raw password reset tokens

Prefer storing hashes or opaque token IDs.

### Keep Redis private

Current `docker-compose.yml` does not expose Redis ports, which is good. Keep Redis accessible only inside the application network in production unless there is a strong operational reason.

### Add security-focused tests

Recommended tests:

- Startup fails when `JWT_SECRET` is missing.
- Startup fails when `JWT_SECRET` is too short.
- Token without `jti` is rejected.
- Revoked `jti` is rejected.
- Refresh token Redis key does not contain the raw token.
- Refresh token TTL matches refresh token expiration.
- Blacklist TTL matches remaining access token lifetime.
- Refresh rotation revokes the old refresh token.

## Remediation Checklist

- [x] Add JWT secret strength validation.
- [x] Change Redis auth eviction policy from `allkeys-lru` to `noeviction`, or split auth Redis from general cache Redis.
- [x] Reject access tokens missing `jti`.
- [x] Add refresh token rotation service method.
- [ ] Ensure refresh endpoint revokes the old refresh token before issuing a new one.
- [x] Remove or disable `SaveToken` unless required.
- [ ] Add login rate limiting before exposing login publicly.
- [ ] Add tests for JWT validation, Redis TTL behavior, blacklist behavior, and refresh rotation.

## Remediation Notes

Applied changes:

- `JwtSettings` now rejects secrets shorter than 32 bytes after base64 decoding or UTF-8 conversion.
- JWT bearer validation now rejects tokens that do not include a non-empty `jti`.
- JWT bearer `SaveToken` is disabled.
- Redis eviction policy in `docker-compose.yml` is now `noeviction`.
- `IAuthService.RotateRefreshTokenAsync(...)` was added.
- `AuthService.RotateRefreshTokenAsync(...)` validates the current refresh token, revokes it, then creates and stores a new session.
- Application tests now cover weak and strong JWT secret validation.

Remaining items:

- A real refresh endpoint still needs to call `RotateRefreshTokenAsync(...)`.
- Login rate limiting should be wired into the future login endpoint before it becomes public.
- Redis integration tests still need a running Redis instance or test container setup.
