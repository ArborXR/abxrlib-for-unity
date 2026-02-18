# Review: Auth & Data Flow – ArborInsightService vs Direct REST (abxrlib-for-unity-nick)

## Summary

- **Auth:** One path goes through `ArborInsightServiceClient` (set fields on service, then `AuthRequest()`); the other sends the same logical payload via `UnityWebRequest` to `/v1/auth/token`. Both use the same payload source (`LoadConfigIntoPayload()` + `GetArborData()` + `ValidateConfigValues()`).
- **Data:** When `UsingArborInsightServiceForData()` is true, events/telemetry/logs/storage go to the service (Deferred APIs); otherwise they are queued and sent via HTTP to `/v1/collect/data` and `/v1/storage`. The session never switches from one mode to the other.
- **Config:** Both paths use the same validation and config sources; when the service is used for auth, config can come from `GetAppConfig()` or fallback HTTP with `SetAuthHeaders`.

Below: differences, bugs found (and one fix applied), and recommendations to keep service vs direct behavior aligned.

---

## 1. Flow comparison

### 1.1 Authentication

| Step | ArborInsightService path (Android, service ready) | Direct REST path |
|------|---------------------------------------------------|------------------|
| Payload source | `LoadConfigIntoPayload()` then `GetArborData()` (and intent/query overrides) | Same |
| Validation | `ValidateConfigValues()` (appToken/orgToken or appId/orgId/authSecret) | Same |
| buildType to API | `production_custom` → `production` before sending | Same (in JSON body) |
| Sending | Push each `AuthPayload` field to service via bridge `set_*`, then `AuthRequest(userId, authMechanismDict)` | `UnityWebRequest` POST with `JsonConvert.SerializeObject(_payload)` |
| Session id | **Not** pushed to service (no `set_SessionId` in bridge); service uses its own | Sent in JSON as `_payload.sessionId` |
| Response | Parse service auth JSON into `ResponseData`; set `_usedArborInsightServiceForSession = true` | `ParseAuthResponse(request.downloadHandler.text)` |

So the only intentional difference in “what we send” is **sessionId**: direct path sends Unity’s `_payload.sessionId`; service path does not set it on the service, so the service uses its own (e.g. from `refreshSessionId()` in ArborLibClient). If the backend expects the same session_id for both paths, the AAR would need to expose `set_SessionId` and Unity would need to push `_payload.sessionId` in the service branch.

### 1.2 Config fetch (after first auth)

| Step | When service was used for auth | Direct (or fallback) |
|------|--------------------------------|----------------------|
| Source | Prefer `ArborInsightServiceClient.GetAppConfig()` | `UnityWebRequest` GET `/v1/storage/config` with `SetAuthHeaders(request)` |
| Condition | `ServiceIsFullyInitialized()` (not `UsingArborInsightServiceForData()`) | When `GetAppConfig()` is empty or service not used |

So config is consistent: we use service config when the service is available; otherwise we use HTTP with the same auth headers built from `ResponseData` (which was set from either the service auth response or the direct auth response).

### 1.3 Data sending (events, telemetry, logs, storage)

| Aspect | ArborInsightService path | Direct path |
|--------|---------------------------|-------------|
| When | `_authService.UsingArborInsightServiceForData()` is true (set once when auth succeeds via service) | Otherwise, for the whole session |
| Events | `ArborInsightServiceClient.EventDeferred(name, meta)` | Queued; `Send()` → POST `/v1/collect/data` with `SetAuthHeaders(request, json)` |
| Telemetry | `AddTelemetryEntryDeferred(name, meta)` | Queued; same POST |
| Logs | `Log*Deferred(text, dict)` by level | Queued; same POST |
| Storage | `StorageSetDefaultEntryFromString` / `StorageSetEntryFromString` | Queued; POST `/v1/storage` with auth headers |

No inconsistency found: the same logical data is either sent via the service or via HTTP; the only difference is transport and who holds the token (service vs Unity’s `ResponseData`).

---

## 2. Bugs and fixes

### 2.1 GetArborData() guard (fixed)

**Issue:** The guard was written as:

```csharp
if (!_arborServiceClient?.IsConnected() == true) return;
```

When `_arborServiceClient` is **null**, `?.IsConnected()` is `null` (bool?), and `!null` is `null`; `null == true` is false, so we did **not** return and still ran `GetArborData()`. That could overwrite `_payload` (e.g. orgToken, orgId, authSecret) with empty or wrong values from `Abxr.GetOrgId()` / `Abxr.GetFingerprint()` when there is no connected Arbor client.

**Fix (applied):** Only run Arbor overrides when the client exists and is connected:

```csharp
if (_arborServiceClient?.IsConnected() != true) return;
```

So we return when the client is null or not connected, and only apply Arbor SDK overrides when `IsConnected()` is true.

### 2.2 sessionId not pushed to service (design / “limit differences”)

**Observation:** In `AuthRequestCoroutine`, the service path iterates `AuthPayload` and pushes many fields to the service but has **no case for `sessionId`**. The direct path sends the full `_payload` (including `sessionId`) in the JSON body. The service (ArborLibClient) uses its own `sessionId` (e.g. from `refreshSessionId()`).

**Impact:** Backend may see different session identifiers for the same logical session when using the service vs direct. If product requires one consistent session id across both paths, the AAR would need something like `set_SessionId` and Unity should set it from `_payload.sessionId` in the same place it sets other auth fields.

**Recommendation:** If “limit areas of code differences” includes “same session_id in both paths”, add `set_SessionId` to the AAR and call it from the service auth block in `AuthRequestCoroutine` (and document that the service must use this value for the auth request).

### 2.3 SSOAccessToken not pushed in service auth branch

**Observation:** `AuthPayload` has `SSOAccessToken` and the bridge has `set_SSOAccessToken`, but the reflection-driven switch in `AuthRequestCoroutine` has no case for `nameof(AuthPayload.SSOAccessToken)` (it falls through to `default`). The direct REST path sends the full serialized `_payload`, so SSOAccessToken is sent there.

**Impact:** If you use SSO access token auth, the service path will not send it unless you add a case (e.g. `case nameof(AuthPayload.SSOAccessToken):` and call `ArborInsightServiceClient.set_SSOAccessToken(...)`).

---

## 3. appId / orgId / authSecret vs appToken / orgToken

### 3.1 Where they come from

- **Config:** `Utils.ExtractConfigData(Configuration.Instance)`:
  - **useAppTokens:** `appToken` required; `orgToken` set from config for non-production; for `buildType == "production"`, `orgToken` is cleared (filled at runtime by GetArborData, intent, or query).
  - **Legacy:** `appID` required; `orgID` / `authSecret` only for `development` or `production_custom`.
- **Runtime overrides:** `GetArborData()` (when Arbor client is connected):
  - **useAppTokens:** can set `_payload.orgToken` from `Utils.BuildOrgTokenDynamic(GetOrgId(), GetFingerprint())`.
  - **Legacy:** sets `_payload.orgId` and `_payload.authSecret` from Arbor SDK.

So both auth modes are fed from the same config and the same Arbor overrides; the only difference is which fields are required and how they’re validated.

### 3.2 Validation

- **ValidateConfigValues():**
  - **useAppTokens:** Requires non-empty, JWT-shaped `appToken`. For `development`, empty `orgToken` is filled with `appToken`; then `orgToken` is required and must look like a JWT. `production_custom` requires orgToken in config (already enforced in `Configuration.IsValid()`).
  - **Legacy:** Requires non-empty `appId`, `orgId`, `authSecret`.

No inconsistency: the same payload that passes validation is what gets sent (either to the service or in the REST body). The service path pushes the same validated `_payload` fields via the bridge.

### 3.3 Possible pitfall

If someone sets **both** `useAppTokens` and legacy fields in Configuration, `ExtractConfigData` and `LoadConfigIntoPayload()` only use one mode (useAppTokens vs legacy). The service path pushes **all** set fields from `_payload` (appId, orgId, authSecret, appToken, orgToken, etc.). So the service might receive both appToken and appId/orgId/authSecret. Backend/AAR behavior in that mixed case should be clearly defined (e.g. “app token wins” or “reject”) so both paths behave the same.

---

## 4. Recommendations to limit code differences

1. **Unify payload building:** Keep using a single `_payload` and the same `LoadConfigIntoPayload()` + `GetArborData()` + `ValidateConfigValues()` for both paths (already the case). Avoid building a second, service-specific payload.
2. **Push sessionId when possible:** Add `set_SessionId` to the AAR and call it from the service auth block so the same `_payload.sessionId` is used as in the REST body, unless the product explicitly wants the service to own session id.
3. **Shared serialization / field list:** The service path currently uses reflection over `AuthPayload` and a long switch. Consider driving the “set on service” list from the same DTO so new fields (e.g. sessionId, SSOAccessToken) are not forgotten on one path. SSOAccessToken is already set via `set_SSOAccessToken` in the bridge; ensure it’s set from `_payload.SSOAccessToken` in the service branch if you use it.
4. **Config fetch:** Using `ServiceIsFullyInitialized()` to decide between `GetAppConfig()` and HTTP is fine. If you want to align with “we used the service for auth”, you could use `UsingArborInsightServiceForData()` for the “prefer GetAppConfig” branch, but that’s optional since the service is typically still initialized when config is fetched.

---

## 5. Files touched in this review

- **AbxrAuthService.cs:** GetArborData guard fixed; rest of flow reviewed (AuthRequestCoroutine, GetConfigurationCoroutine, LoadConfigIntoPayload, GetConfigData, GetArborData, ValidateConfigValues).
- **AbxrDataService.cs, AbxrStorageService.cs:** Confirmed use of `UsingArborInsightServiceForData()` and that service vs HTTP behavior is consistent.
- **Configuration.cs, Utils.ExtractConfigData:** Confirmed appToken/orgToken vs appId/orgId/authSecret handling and validation.

No other code changes were required for the bugs identified above beyond the GetArborData fix.
