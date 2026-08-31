import * as Location from 'expo-location';

import type { LatLng } from './api/freight';

/**
 * Forward-geocode a free-text address to a coordinate. Uses the platform geocoder
 * (Apple on iOS, android.location.Geocoder on Android) — no API key required.
 * Returns null on an empty/short query, a miss, or when the geocoder is unavailable
 * (e.g. some Android emulators); callers fall back to letting the user pin manually.
 */
export async function geocodeAddress(address: string): Promise<LatLng | null> {
  const query = address.trim();
  if (query.length < 5) return null;

  try {
    const [first] = await Location.geocodeAsync(query);
    return first ? { lat: first.latitude, lng: first.longitude } : null;
  } catch {
    return null;
  }
}
