import { useQuery } from '@tanstack/react-query';
import { FlatList, Pressable, Text, View } from 'react-native';

import { Heading, Muted, Screen } from '@/components/ui';
import { useAuthStore } from '@/features/auth/store';
import { freightApi } from '@/services/api/freight';

import type { SharedStackScreenProps } from '../navigation';

export function HistoryScreen({ navigation }: SharedStackScreenProps<'History'>) {
  const isClient = useAuthStore((s) => s.user?.roles ?? []).includes('client');

  const requests = useQuery({
    queryKey: ['requests'],
    queryFn: () => freightApi.listRequests(),
    enabled: isClient,
  });
  const trips = useQuery({ queryKey: ['trips'], queryFn: () => freightApi.listTrips() });

  return (
    <Screen>
      <Heading>Histórico</Heading>
      {isClient ? (
        <FlatList
          data={requests.data?.items ?? []}
          keyExtractor={(r) => r.id}
          ListEmptyComponent={<Muted>Nenhuma requisição ainda.</Muted>}
          renderItem={({ item }) => (
            <Pressable
              className="border-b border-neutral-100 py-3"
              onPress={() => navigation.navigate('Tracking', { requestId: item.id })}
            >
              <Text className="font-medium text-neutral-900">
                {item.originAddress} → {item.destinationAddress}
              </Text>
              <Muted>
                {item.status} · {item.estimate.currency} {item.estimate.amount.toFixed(2)}
              </Muted>
            </Pressable>
          )}
        />
      ) : (
        <FlatList
          data={trips.data?.items ?? []}
          keyExtractor={(t) => t.id}
          ListEmptyComponent={<Muted>Nenhum transporte ainda.</Muted>}
          renderItem={({ item }) => (
            <Pressable
              className="border-b border-neutral-100 py-3"
              onPress={() => navigation.navigate('Trip', { tripId: item.id })}
            >
              <Text className="font-medium text-neutral-900">
                {item.currency} {item.agreedAmount.toFixed(2)}
              </Text>
              <Muted>{item.status}</Muted>
            </Pressable>
          )}
        />
      )}
      <View className="h-2" />
    </Screen>
  );
}
