import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Pressable, Text, View } from 'react-native';

import { Button, Heading, Muted, Screen } from '@/components/ui';
import { freightApi } from '@/services/api/freight';

import type { ProStackScreenProps } from '../navigation';

function nextDays(count: number): string[] {
  return Array.from({ length: count }, (_, i) => {
    const d = new Date();
    d.setDate(d.getDate() + i + 1);
    return d.toISOString().slice(0, 10);
  });
}

export function ScheduleScreen({ navigation }: ProStackScreenProps<'Schedule'>) {
  const qc = useQueryClient();
  const days = nextDays(14);

  const availability = useQuery({
    queryKey: ['availability'],
    queryFn: () => freightApi.getAvailability(),
  });
  const selected = new Set(availability.data ?? []);

  const save = useMutation({
    mutationFn: (dates: string[]) => freightApi.setAvailability(dates),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['availability'] }),
  });

  const toggle = (date: string) => {
    const next = new Set(selected);
    if (next.has(date)) next.delete(date);
    else next.add(date);
    save.mutate([...next]);
  };

  const offers = useQuery({
    queryKey: ['schedule-offers'],
    queryFn: () => freightApi.scheduleOffersInbox(),
    refetchInterval: 5000,
  });

  return (
    <Screen>
      <Heading>Minha agenda</Heading>
      <Muted>Marque os dias em que aceita agendamentos.</Muted>

      <View className="mt-4 flex-row flex-wrap gap-2">
        {days.map((d) => (
          <Pressable
            key={d}
            onPress={() => toggle(d)}
            className={[
              'rounded-full border px-3 py-2',
              selected.has(d) ? 'border-brand bg-brand' : 'border-neutral-300',
            ].join(' ')}
          >
            <Text className={selected.has(d) ? 'text-xs text-white' : 'text-xs text-neutral-700'}>
              {d.slice(5)}
            </Text>
          </Pressable>
        ))}
      </View>

      <Heading>Agendamentos disponíveis</Heading>
      {(offers.data ?? []).length === 0 && <Muted>Nenhum agendamento no momento.</Muted>}
      {(offers.data ?? []).map((o) => (
        <View key={o.id} className="mt-3 rounded-lg border border-neutral-200 p-4">
          <Text className="font-medium text-neutral-900">
            {o.scheduledDate} · {o.weightKg} kg
          </Text>
          <View className="mt-2">
            <Button
              title="Aceitar agendamento"
              onPress={async () => {
                const { tripId } = await freightApi.acceptScheduleOffer(o.id);
                await offers.refetch();
                navigation.navigate('Trip', { tripId });
              }}
            />
          </View>
        </View>
      ))}
    </Screen>
  );
}
