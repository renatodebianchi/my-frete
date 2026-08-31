import { useMutation } from '@tanstack/react-query';
import * as Crypto from 'expo-crypto';
import { useState } from 'react';
import { ScrollView, Text, View } from 'react-native';

import { Button, ErrorText, Field, Heading, Muted, Screen } from '@/components/ui';
import { MapPicker } from '@/components/MapPicker';
import { useAddressPin, type AddressPinStatus } from '@/features/requests/useAddressPin';
import { ApiError } from '@/services/api/client';
import { freightApi, type LatLng, type PriceEstimate } from '@/services/api/freight';

import type { ClientStackScreenProps } from '../navigation';

function PinHint({ status }: { status: AddressPinStatus }) {
  if (status === 'locating') {
    return (
      <Text className="mb-3 -mt-2 text-xs text-neutral-400">Localizando endereço no mapa…</Text>
    );
  }
  if (status === 'found') {
    return <Text className="mb-3 -mt-2 text-xs text-brand">Endereço marcado no mapa abaixo.</Text>;
  }
  if (status === 'notFound') {
    return (
      <Text className="mb-3 -mt-2 text-xs text-amber-600">
        Endereço não encontrado — toque no mapa para marcar.
      </Text>
    );
  }
  return null;
}

export function NewRequestScreen({ navigation }: ClientStackScreenProps<'NewRequest'>) {
  const [itemText, setItemText] = useState('');
  const [weight, setWeight] = useState('');
  const [originText, setOriginText] = useState('');
  const [origin, setOrigin] = useState<LatLng | null>(null);
  const [destText, setDestText] = useState('');
  const [dest, setDest] = useState<LatLng | null>(null);
  const [estimate, setEstimate] = useState<PriceEstimate | null>(null);
  const [error, setError] = useState<string | null>(null);

  const originStatus = useAddressPin(originText, (point) => {
    setOrigin(point);
    setEstimate(null);
  });
  const destStatus = useAddressPin(destText, (point) => {
    setDest(point);
    setEstimate(null);
  });

  const ready = itemText && Number(weight) > 0 && originText && origin && destText && dest;

  const estimateMutation = useMutation({
    mutationFn: () =>
      freightApi.estimate(
        { text: originText, point: origin! },
        { text: destText, point: dest! },
        Number(weight),
      ),
    onSuccess: setEstimate,
    onError: (e) => setError(e instanceof ApiError ? e.message : 'Não foi possível estimar.'),
  });

  const createMutation = useMutation({
    mutationFn: () =>
      freightApi.createRequest({
        items: [{ description: itemText, quantity: 1 }],
        estimatedWeightKg: Number(weight),
        origin: { text: originText, point: origin! },
        destination: { text: destText, point: dest! },
        idempotencyKey: Crypto.randomUUID(),
      }),
    onSuccess: ({ id }) => navigation.replace('Tracking', { requestId: id }),
    onError: (e) =>
      setError(e instanceof ApiError ? e.message : 'Não foi possível criar a requisição.'),
  });

  return (
    <Screen>
      <ScrollView showsVerticalScrollIndicator={false}>
        <Heading>Nova requisição</Heading>
        <View className="mt-4">
          <Field label="O que vai transportar?" value={itemText} onChangeText={setItemText} />
          <Field
            label="Peso estimado (kg)"
            keyboardType="numeric"
            value={weight}
            onChangeText={(v) => {
              setWeight(v);
              setEstimate(null);
            }}
          />
          <Field
            label="Endereço de origem"
            placeholder="Rua, número, cidade"
            value={originText}
            onChangeText={setOriginText}
          />
          <PinHint status={originStatus} />
          <MapPicker label="Origem no mapa" value={origin} onChange={setOrigin} />
          <Field
            label="Endereço de destino"
            placeholder="Rua, número, cidade"
            value={destText}
            onChangeText={setDestText}
          />
          <PinHint status={destStatus} />
          <MapPicker label="Destino no mapa" value={dest} onChange={setDest} />

          <ErrorText>{error}</ErrorText>

          {estimate ? (
            <View className="mb-4 rounded-lg bg-neutral-100 p-4">
              <Text className="text-2xl font-semibold text-neutral-900">
                {estimate.currency} {estimate.amount.toFixed(2)}
              </Text>
              <Muted>
                Estimativa · {estimate.distanceKm} km
                {estimate.distanceSource === 'geodesic_fallback' ? ' (aproximada)' : ''}
              </Muted>
              <View className="mt-3">
                <Button
                  title="Confirmar e buscar profissional"
                  onPress={() => createMutation.mutate()}
                  loading={createMutation.isPending}
                />
              </View>
            </View>
          ) : (
            <Button
              title="Ver estimativa"
              onPress={() => {
                setError(null);
                estimateMutation.mutate();
              }}
              loading={estimateMutation.isPending}
              disabled={!ready}
            />
          )}
        </View>
      </ScrollView>
    </Screen>
  );
}
