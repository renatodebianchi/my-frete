import { useQuery } from '@tanstack/react-query';
import { useEffect, useState } from 'react';
import { Switch, View } from 'react-native';

import { Button, ErrorText, Heading, Muted, Screen } from '@/components/ui';
import { useAuthStore } from '@/features/auth/store';
import { ApiError } from '@/services/api/client';
import { professionalApi } from '@/services/api/auth';
import { freightApi } from '@/services/api/freight';
import { startLocationUpdates, stopLocationUpdates } from '@/services/location';

import type { ProStackScreenProps } from '../navigation';

export function ProHomeScreen({ navigation }: ProStackScreenProps<'ProHome'>) {
  const user = useAuthStore((s) => s.user);
  const logout = useAuthStore((s) => s.logout);
  const pro = user?.professional;

  const [available, setAvailable] = useState(pro?.immediateAvailability ?? false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (available) void startLocationUpdates();
    else stopLocationUpdates();
    return () => stopLocationUpdates();
  }, [available]);

  const { data: inbox } = useQuery({
    queryKey: ['offers-inbox'],
    queryFn: () => freightApi.offersInbox(),
    refetchInterval: available ? 3000 : false,
    enabled: available,
  });

  useEffect(() => {
    if (inbox && inbox.length > 0) navigation.navigate('IncomingOffer');
  }, [inbox, navigation]);

  const toggle = async (next: boolean) => {
    setError(null);
    setBusy(true);
    setAvailable(next);
    try {
      if (next && !(await startLocationUpdates())) {
        throw new ApiError(
          0,
          'location.denied',
          'Permita o acesso à localização para ficar disponível.',
          null,
        );
      }
      const updated = await professionalApi.update({ immediateAvailability: next });
      useAuthStore.setState((s) =>
        s.user ? { user: { ...s.user, professional: { ...s.user.professional!, ...updated } } } : s,
      );
    } catch (e) {
      setAvailable(!next);
      stopLocationUpdates();
      setError(e instanceof ApiError ? e.message : 'Não foi possível atualizar.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <Screen>
      <View className="flex-1">
        <Heading>Painel do profissional</Heading>
        <Muted>
          Carga máxima {pro?.maxLoadKg ?? '—'} kg · verificação {pro?.verificationStatus ?? '—'}
        </Muted>

        <View className="mt-8 flex-row items-center justify-between rounded-lg border border-neutral-200 px-4 py-4">
          <Muted>Disponível para fretes</Muted>
          <Switch value={available} onValueChange={toggle} disabled={busy} />
        </View>

        <ErrorText>{error}</ErrorText>

        <View className="mt-4 gap-3">
          <Button
            title="Ofertas"
            variant="ghost"
            onPress={() => navigation.navigate('IncomingOffer')}
          />
          <Button
            title="Minha agenda"
            variant="ghost"
            onPress={() => navigation.navigate('Schedule')}
          />
          <Button
            title="Meus transportes"
            variant="ghost"
            onPress={() => navigation.navigate('History')}
          />
        </View>
      </View>
      <Button title="Sair" variant="ghost" onPress={() => void logout()} />
    </Screen>
  );
}
