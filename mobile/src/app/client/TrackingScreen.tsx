import { useQuery } from '@tanstack/react-query';
import { Text, View } from 'react-native';

import { Button, Heading, Muted, Screen } from '@/components/ui';
import { freightApi } from '@/services/api/freight';

import type { ClientStackScreenProps } from '../navigation';

function futureDate(days: number): string {
  const d = new Date();
  d.setDate(d.getDate() + days);
  return d.toISOString().slice(0, 10);
}

const LABELS: Record<string, string> = {
  searching: 'Procurando um profissional…',
  hired: 'Profissional a caminho',
  awaiting_schedule_decision: 'Ninguém aceitou agora',
  scheduled_searching: 'Procurando para a data escolhida…',
  scheduled: 'Agendado',
  completed: 'Transporte concluído',
  unfulfilled: 'Não foi possível atender',
  cancelled: 'Cancelada',
};

const ONGOING = ['searching', 'scheduled_searching', 'hired'];

export function TrackingScreen({ navigation, route }: ClientStackScreenProps<'Tracking'>) {
  const { requestId } = route.params;

  const { data, refetch } = useQuery({
    queryKey: ['request', requestId],
    queryFn: () => freightApi.getRequest(requestId),
    refetchInterval: (q) => (ONGOING.includes(q.state.data?.status ?? '') ? 3000 : false),
  });

  const linked = ['hired', 'completed'].includes(data?.status ?? '');
  const { data: trips } = useQuery({
    queryKey: ['trips'],
    queryFn: () => freightApi.listTrips(),
    enabled: linked,
  });
  const trip = trips?.items.find((t) => t.requestId === requestId);

  const canCancel =
    data &&
    ['searching', 'scheduled_searching', 'awaiting_schedule_decision'].includes(data.status);

  return (
    <Screen>
      <View className="flex-1">
        <Heading>{data ? (LABELS[data.status] ?? data.status) : 'Carregando…'}</Heading>
        {data && (
          <Muted>
            {data.originAddress} → {data.destinationAddress} · {data.estimate.currency}{' '}
            {data.estimate.amount.toFixed(2)}
          </Muted>
        )}

        {data?.status === 'awaiting_schedule_decision' && (
          <View className="mt-6">
            <Text className="mb-2 font-medium text-neutral-900">
              Deseja agendar para outro dia?
            </Text>
            <View className="gap-2">
              {[2, 3, 7].map((d) => (
                <Button
                  key={d}
                  title={`Agendar para ${futureDate(d)}`}
                  variant="ghost"
                  onPress={async () => {
                    await freightApi.scheduleDecision(requestId, 'schedule', futureDate(d));
                    await refetch();
                  }}
                />
              ))}
              <Button
                title="Não agendar"
                onPress={async () => {
                  await freightApi.scheduleDecision(requestId, 'decline');
                  await refetch();
                }}
              />
            </View>
          </View>
        )}
        {data?.status === 'scheduled_searching' && (
          <View className="mt-6">
            <Muted>Procurando um profissional para a data escolhida…</Muted>
          </View>
        )}
        {trip && (
          <View className="mt-6">
            <Button
              title="Ver transporte"
              onPress={() => navigation.navigate('Trip', { tripId: trip.id })}
            />
          </View>
        )}
      </View>

      {canCancel && (
        <Button
          title="Cancelar requisição"
          variant="ghost"
          onPress={async () => {
            await freightApi.cancelRequest(requestId);
            await refetch();
          }}
        />
      )}
    </Screen>
  );
}
