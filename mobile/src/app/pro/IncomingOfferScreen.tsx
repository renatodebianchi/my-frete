import { useQuery } from '@tanstack/react-query';
import { useEffect, useState } from 'react';
import { View } from 'react-native';

import { Button, ErrorText, Heading, Muted, Screen } from '@/components/ui';
import { ApiError } from '@/services/api/client';
import { freightApi } from '@/services/api/freight';

import type { ProStackScreenProps } from '../navigation';

function secondsLeft(respondBy: string, now: number): number {
  return Math.max(0, Math.round((new Date(respondBy).getTime() - now) / 1000));
}

export function IncomingOfferScreen({ navigation }: ProStackScreenProps<'IncomingOffer'>) {
  const [now, setNow] = useState(Date.now());
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    const t = setInterval(() => setNow(Date.now()), 1000);
    return () => clearInterval(t);
  }, []);

  const { data, refetch } = useQuery({
    queryKey: ['offers-inbox'],
    queryFn: () => freightApi.offersInbox(),
    refetchInterval: 3000,
  });

  const offer = data?.[0];
  const left = offer ? secondsLeft(offer.respondBy, now) : 0;

  useEffect(() => {
    if (offer && left <= 0) void refetch();
  }, [offer, left, refetch]);

  const accept = async () => {
    if (!offer) return;
    setBusy(true);
    setError(null);
    try {
      const { tripId } = await freightApi.acceptOffer(offer.id);
      navigation.replace('Trip', { tripId });
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Não foi possível aceitar.');
      void refetch();
    } finally {
      setBusy(false);
    }
  };

  const decline = async () => {
    if (!offer) return;
    setBusy(true);
    try {
      await freightApi.declineOffer(offer.id);
    } finally {
      setBusy(false);
      await refetch();
    }
  };

  if (!offer) {
    return (
      <Screen>
        <View className="flex-1 items-center justify-center">
          <Muted>Nenhuma oferta no momento.</Muted>
          <View className="mt-4">
            <Button title="Voltar" variant="ghost" onPress={() => navigation.goBack()} />
          </View>
        </View>
      </Screen>
    );
  }

  return (
    <Screen>
      <View className="flex-1">
        <Heading>Oferta de frete</Heading>
        <Muted>
          {offer.summary.originAddress} → {offer.summary.destinationAddress}
        </Muted>
        <View className="mt-6 rounded-lg bg-neutral-100 p-4">
          <Muted>Distância {offer.summary.distanceKm} km · {offer.summary.estimatedWeightKg} kg</Muted>
          <Muted>Valor estimado: BRL {offer.summary.estimatedAmount.toFixed(2)}</Muted>
        </View>
        <View className="mt-6 items-center">
          <Heading>{left}s</Heading>
          <Muted>para responder</Muted>
        </View>
        <ErrorText>{error}</ErrorText>
      </View>

      <View className="gap-3">
        <Button title="Aceitar" onPress={accept} loading={busy} disabled={left <= 0} />
        <Button title="Recusar" variant="ghost" onPress={decline} disabled={busy} />
      </View>
    </Screen>
  );
}
