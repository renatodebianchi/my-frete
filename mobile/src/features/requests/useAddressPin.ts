import { useEffect, useRef, useState } from 'react';

import type { LatLng } from '@/services/api/freight';
import { geocodeAddress } from '@/services/geocode';

export type AddressPinStatus = 'idle' | 'locating' | 'found' | 'notFound';

/**
 * Debounced forward-geocode that links an address field to a map pin: as the user
 * types an origin/destination address, resolve it to a coordinate and hand it back
 * so the caller can drop the pin on the map.
 *
 * Manual map taps still win — they change the point without touching the address
 * text, so this effect does not re-run and overwrite them.
 */
export function useAddressPin(
  address: string,
  onResolved: (point: LatLng) => void,
): AddressPinStatus {
  const [status, setStatus] = useState<AddressPinStatus>('idle');
  const onResolvedRef = useRef(onResolved);
  onResolvedRef.current = onResolved;
  const resolvedQuery = useRef('');

  useEffect(() => {
    const query = address.trim();
    if (query.length < 5 || query === resolvedQuery.current) return;

    let cancelled = false;
    setStatus('locating');
    const timer = setTimeout(async () => {
      const point = await geocodeAddress(query);
      if (cancelled) return;
      resolvedQuery.current = query;
      if (point) {
        onResolvedRef.current(point);
        setStatus('found');
      } else {
        setStatus('notFound');
      }
    }, 800);

    return () => {
      cancelled = true;
      clearTimeout(timer);
    };
  }, [address]);

  return status;
}
