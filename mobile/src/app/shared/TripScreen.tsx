import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { View } from 'react-native';

import { Button, ErrorText, Field, Heading, Muted, Screen } from '@/components/ui';
import { useAuthStore } from '@/features/auth/store';
import { ApiError } from '@/services/api/client';
import { freightApi } from '@/services/api/freight';

import type { SharedStackScreenProps } from '../navigation';

const STATUS: Record<string, string> = {
  contratada: 'Contratado',
  em_andamento: 'Em andamento',
  entregue: 'Entregue — aguardando o cliente',
  confirmada: 'Concluído e confirmado',
  contestada: 'Entrega contestada',
  cancelada: 'Cancelado',
};

export function TripScreen({ route }: SharedStackScreenProps<'Trip'>) {
  const { tripId } = route.params;
  const roles = useAuthStore((s) => s.user?.roles ?? []);
  const qc = useQueryClient();
  const [amount, setAmount] = useState('');
  const [error, setError] = useState<string | null>(null);

  const { data: trip } = useQuery({
    queryKey: ['trip', tripId],
    queryFn: () => freightApi.getTrip(tripId),
    refetchInterval: (q) =>
      ['contratada', 'em_andamento', 'entregue'].includes(q.state.data?.status ?? '') ? 4000 : false,
  });

  if (!trip) {
    return (
      <Screen>
        <Muted>Carregando…</Muted>
      </Screen>
    );
  }

  const run = async (fn: () => Promise<unknown>) => {
    setError(null);
    try {
      await fn();
      await qc.invalidateQueries({ queryKey: ['trip', tripId] });
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Ação falhou.');
    }
  };

  const isPro = roles.includes('professional') && !roles.includes('client');
  const editable = trip.status === 'contratada' || trip.status === 'em_andamento';
  const isClientView = roles.includes('client');

  return (
    <Screen>
      <View className="flex-1">
        <Heading>{STATUS[trip.status] ?? trip.status}</Heading>
        <Muted>
          Valor combinado: {trip.currency} {trip.agreedAmount.toFixed(2)}
          {trip.paymentSettledOutsideApp ? ' · pago fora do app' : ''}
        </Muted>

        {editable && (
          <View className="mt-6">
            <Field
              label="Ajustar valor combinado"
              keyboardType="numeric"
              value={amount}
              onChangeText={setAmount}
              placeholder={trip.agreedAmount.toFixed(2)}
            />
            <Button
              title="Salvar valor"
              variant="ghost"
              onPress={() => run(() => freightApi.setAgreedAmount(tripId, Number(amount)))}
              disabled={!Number(amount)}
            />
          </View>
        )}

        <ErrorText>{error}</ErrorText>
      </View>

      <View className="gap-3">
        {isPro && (trip.status === 'contratada' || trip.status === 'em_andamento') && (
          <Button title="Marcar como entregue" onPress={() => run(() => freightApi.deliverTrip(tripId))} />
        )}
        {isClientView && trip.status === 'entregue' && (
          <>
            <Button title="Confirmar recebimento" onPress={() => run(() => freightApi.clientRespond(tripId, 'confirm'))} />
            <Button
              title="Contestar entrega"
              variant="ghost"
              onPress={() => run(() => freightApi.clientRespond(tripId, 'dispute'))}
            />
          </>
        )}
      </View>
    </Screen>
  );
}
