import * as Location from 'expo-location';

import { professionalApi } from './api/auth';

const MIN_INTERVAL_MS = 60_000;

let subscription: Location.LocationSubscription | null = null;
let lastSentAt = 0;

/**
 * Streams the professional's location to the API while they are available for immediate offers
 * (FR-012a). Throttled to one update per minute; safe to call repeatedly.
 */
export async function startLocationUpdates(): Promise<boolean> {
  if (subscription) return true;

  const { granted } = await Location.requestForegroundPermissionsAsync();
  if (!granted) return false;

  subscription = await Location.watchPositionAsync(
    { accuracy: Location.Accuracy.Balanced, timeInterval: MIN_INTERVAL_MS, distanceInterval: 100 },
    (position) => {
      const now = Date.now();
      if (now - lastSentAt < MIN_INTERVAL_MS) return;
      lastSentAt = now;
      void professionalApi
        .updateLocation(position.coords.latitude, position.coords.longitude)
        .catch(() => {
          // best effort — next tick retries
        });
    },
  );

  return true;
}

export function stopLocationUpdates(): void {
  subscription?.remove();
  subscription = null;
  lastSentAt = 0;
}
