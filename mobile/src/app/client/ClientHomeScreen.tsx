import { View } from 'react-native';

import { Button, Heading, Muted, Screen } from '@/components/ui';
import { useAuthStore } from '@/features/auth/store';

import type { ClientStackScreenProps } from '../navigation';

export function ClientHomeScreen({ navigation }: ClientStackScreenProps<'ClientHome'>) {
  const user = useAuthStore((s) => s.user);
  const logout = useAuthStore((s) => s.logout);

  return (
    <Screen>
      <View className="flex-1">
        <Heading>Olá, {user?.name.split(' ')[0] ?? 'cliente'}</Heading>
        <Muted>Peça um mini-frete perto de você.</Muted>
        <View className="mt-8 gap-3">
          <Button title="Nova requisição" onPress={() => navigation.navigate('NewRequest')} />
          <Button
            title="Histórico"
            variant="ghost"
            onPress={() => navigation.navigate('History')}
          />
        </View>
      </View>
      <Button title="Sair" variant="ghost" onPress={() => void logout()} />
    </Screen>
  );
}
