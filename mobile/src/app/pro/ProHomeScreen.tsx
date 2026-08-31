import { useEffect, useState } from 'react';
import { Switch, View } from 'react-native';

import { Button, ErrorText, Heading, Muted, Screen } from '@/components/ui';
import { useAuthStore } from '@/features/auth/store';
import { ApiError } from '@/services/api/client';
import { professionalApi } from '@/services/api/auth';
import { startLocationUpdates, stopLocationUpdates } from '@/services/location';

export function ProHomeScreen() {
  const user = useAuthStore((s) => s.user);
  const setUser = (updater: (u: NonNullable<typeof user>) => typeof user) =>
    useAuthStore.setState((s) => (s.user ? { user: updater(s.user) } : s));
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

  const toggle = async (next: boolean) => {
    setError(null);
    setBusy(true);
    setAvailable(next);
    try {
      if (next) {
        const ok = await startLocationUpdates();
        if (!ok) throw new ApiError(0, 'location.denied', 'Permita o acesso à localização para ficar disponível.', null);
      }
      const updated = await professionalApi.update({ immediateAvailability: next });
      setUser((u) => ({ ...u, professional: { ...u.professional!, ...updated } }));
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
          <View>
            <Muted>Disponível para fretes</Muted>
          </View>
          <Switch value={available} onValueChange={toggle} disabled={busy} />
        </View>

        <ErrorText>{error}</ErrorText>
        <Muted>Ofertas de transporte chegam na US1.</Muted>
      </View>
      <Button title="Sair" variant="ghost" onPress={() => void logout()} />
    </Screen>
  );
}
