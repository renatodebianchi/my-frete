import { View } from 'react-native';

import { Button, Heading, Muted, Screen } from '@/components/ui';
import { useAuthStore } from '@/features/auth/store';

export function ClientHomeScreen() {
  const user = useAuthStore((s) => s.user);
  const logout = useAuthStore((s) => s.logout);

  return (
    <Screen>
      <View className="flex-1">
        <Heading>Olá, {user?.name.split(' ')[0] ?? 'cliente'}</Heading>
        <Muted>Peça um mini-frete. (Fluxo de requisição chega na US1.)</Muted>
        <View className="mt-6">
          <Button title="Nova requisição" onPress={() => {}} disabled />
        </View>
      </View>
      <Button title="Sair" variant="ghost" onPress={() => void logout()} />
    </Screen>
  );
}
