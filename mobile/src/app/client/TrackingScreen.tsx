import { useQuery } from '@tanstack/react-query';
import { View } from 'react-native';

import { Button, Heading, Muted, Screen } from '@/components/ui';
import { freightApi } from '@/services/api/freight';

import type { ClientStackScreenProps } from '../navigation';

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
    data && ['searching', 'scheduled_searching', 'awaiting_schedule_decision'].includes(data.status);

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
            <Muted>O agendamento chega na próxima versão (US2).</Muted>
          </View>
        )}
        {trip && (
          <View className="mt-6">
            <Button title="Ver transporte" onPress={() => navigation.navigate('Trip', { tripId: trip.id })} />
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
