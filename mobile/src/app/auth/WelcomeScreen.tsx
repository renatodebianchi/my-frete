import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { View } from 'react-native';

import { Button, Heading, Muted, Screen } from '@/components/ui';

import type { AuthStackParamList } from '../navigation';

export function WelcomeScreen({ navigation }: NativeStackScreenProps<AuthStackParamList, 'Welcome'>) {
  return (
    <Screen>
      <View className="flex-1 justify-center">
        <Heading>my-frete</Heading>
        <Muted>Mini-fretes sob demanda, perto de você.</Muted>
      </View>
      <View className="gap-3">
        <Button title="Criar conta" onPress={() => navigation.navigate('Register')} />
        <Button title="Entrar" variant="ghost" onPress={() => navigation.navigate('Login')} />
      </View>
    </Screen>
  );
}
