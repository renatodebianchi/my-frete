import { useState } from 'react';
import { View } from 'react-native';

import { Button, ErrorText, Field, Heading, Screen } from '@/components/ui';
import { useAuthStore } from '@/features/auth/store';
import { ApiError } from '@/services/api/client';

export function LoginScreen() {
  const login = useAuthStore((s) => s.login);
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const submit = async () => {
    setError(null);
    setLoading(true);
    try {
      await login(email.trim(), password);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Não foi possível entrar.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <Screen>
      <Heading>Entrar</Heading>
      <View className="mt-6">
        <Field
          label="E-mail"
          autoCapitalize="none"
          keyboardType="email-address"
          value={email}
          onChangeText={setEmail}
        />
        <Field label="Senha" secureTextEntry value={password} onChangeText={setPassword} />
        <ErrorText>{error}</ErrorText>
        <Button title="Entrar" onPress={submit} loading={loading} disabled={!email || !password} />
      </View>
    </Screen>
  );
}
