import { View } from 'react-native';

import { Button, Heading, Muted, Screen } from '@/components/ui';
import { useAuthStore } from '@/features/auth/store';

export function ProHomeScreen() {
  const user = useAuthStore((s) => s.user);
  const logout = useAuthStore((s) => s.logout);
  const pro = user?.professional;

  return (
    <Screen>
      <View className="flex-1">
        <Heading>Painel do profissional</Heading>
        <Muted>
          Carga máxima {pro?.maxLoadKg ?? '—'} kg · verificação {pro?.verificationStatus ?? '—'}
        </Muted>
        <View className="mt-6">
          <Button title="Ficar disponível" onPress={() => {}} disabled />
        </View>
        <Muted>Ofertas e disponibilidade chegam nas US3/US1.</Muted>
      </View>
      <Button title="Sair" variant="ghost" onPress={() => void logout()} />
    </Screen>
  );
}
