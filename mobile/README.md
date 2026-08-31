# my-frete mobile

Expo (React Native + TypeScript) app for Android and iOS. Not containerized — this is native
build tooling, out of scope for the Docker-first principle.

## Run

```bash
npm install
npm run codegen                 # regenerate the API model from contracts/openapi.yaml
npx expo start --dev-client     # needs a dev build or Expo Go on a device/emulator
```

Set `mobile/.env`:

```
EXPO_PUBLIC_API_BASE_URL=http://10.0.2.2:8080/v1   # Android emulator
# or http://localhost:8080/v1                       # iOS simulator
```

Start the backend first (see `../api/README.md` → `docker compose up`).

## Checks

```bash
npm run typecheck   # tsc --noEmit
npm run lint        # eslint, 0 warnings
npm test            # jest
```

## Structure

- `src/app/` — navigation (`AuthStack` → role-aware `AppStack`) and screens
  (`auth/`, `client/`, `pro/`, `shared/`).
- `src/features/auth/` — zustand store; tokens in `expo-secure-store`; the store wires the HTTP
  client's refresh/session hooks at module load.
- `src/services/api/` — `client.ts` (fetch wrapper: `x-correlation-id`, single refresh-and-retry
  on 401), `auth.ts`, `freight.ts`, `generated.ts` (codegen output, git-ignored).
- `src/services/location.ts` — publishes the professional's location every 60s while available.
- `src/services/push.ts` — permission flow + `POST /accounts/me/devices`.

## Flows covered

Client: sign up → new request (map pickers) → estimate → track → (schedule if not accepted) →
trip → confirm/dispute. Professional: sign up (capacity) → go available → incoming offer
(30s countdown) → accept → deliver; agenda screen for schedule-availability + scheduled offers.
