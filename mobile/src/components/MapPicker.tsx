import { useState } from 'react';
import { Text, View } from 'react-native';
import MapView, { Marker, type MapPressEvent } from 'react-native-maps';

import type { LatLng } from '@/services/api/freight';

const SP = { latitude: -23.5613, longitude: -46.656, latitudeDelta: 0.08, longitudeDelta: 0.08 };

export function MapPicker({
  label,
  value,
  onChange,
}: {
  label: string;
  value: LatLng | null;
  onChange: (point: LatLng) => void;
}) {
  const [region] = useState(SP);

  return (
    <View className="mb-4">
      <Text className="mb-1 text-sm font-medium text-neutral-700">{label}</Text>
      <View className="h-44 overflow-hidden rounded-lg border border-neutral-300">
        <MapView
          style={{ flex: 1 }}
          initialRegion={region}
          onPress={(e: MapPressEvent) =>
            onChange({ lat: e.nativeEvent.coordinate.latitude, lng: e.nativeEvent.coordinate.longitude })
          }
        >
          {value && <Marker coordinate={{ latitude: value.lat, longitude: value.lng }} />}
        </MapView>
      </View>
      <Text className="mt-1 text-xs text-neutral-400">
        {value ? `${value.lat.toFixed(5)}, ${value.lng.toFixed(5)}` : 'Toque no mapa para marcar'}
      </Text>
    </View>
  );
}
