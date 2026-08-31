import { useState } from 'react';
import { Pressable, Text, View } from 'react-native';

import { Button, ErrorText, Field, Heading, Muted, Screen } from '@/components/ui';
import { useAuthStore } from '@/features/auth/store';
import { ApiError } from '@/services/api/client';

type Role = 'client' | 'professional';

export function RegisterScreen() {
  const register = useAuthStore((s) => s.register);
  const [form, setForm] = useState({ name: '', email: '', phone: '', password: '', maxLoadKg: '' });
  const [roles, setRoles] = useState<Role[]>(['client']);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const set = (k: keyof typeof form) => (v: string) => setForm((f) => ({ ...f, [k]: v }));
  const toggleRole = (r: Role) =>
    setRoles((rs) => (rs.includes(r) ? rs.filter((x) => x !== r) : [...rs, r]));

  const validate = (): string | null => {
    if (form.name.trim().length < 2) return 'Informe seu nome completo.';
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(form.email.trim())) return 'Informe um e-mail válido.';
    if (form.phone.trim().replace(/\D/g, '').length < 10) return 'Informe um telefone válido com DDD.';
    if (form.password.length < 8) return 'A senha precisa ter ao menos 8 caracteres.';
    if (roles.includes('professional') && !(Number(form.maxLoadKg) > 0))
      return 'Informe a carga máxima (kg) para o perfil de profissional.';
    return null;
  };

  const submit = async () => {
    setError(null);
    const localError = validate();
    if (localError) {
      setError(localError);
      return;
    }
    setLoading(true);
    try {
      await register({
        name: form.name.trim(),
        email: form.email.trim(),
        phone: form.phone.trim(),
        password: form.password,
        roles,
        maxLoadKg: roles.includes('professional') ? Number(form.maxLoadKg) : undefined,
      });
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Não foi possível criar a conta.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <Screen>
      <Heading>Criar conta</Heading>
      <View className="mt-4">
        <Field label="Nome" value={form.name} onChangeText={set('name')} />
        <Field
          label="E-mail"
          autoCapitalize="none"
          keyboardType="email-address"
          value={form.email}
          onChangeText={set('email')}
        />
        <Field
          label="Telefone"
          keyboardType="phone-pad"
          value={form.phone}
          onChangeText={set('phone')}
        />
        <Field label="Senha" secureTextEntry value={form.password} onChangeText={set('password')} />

        <Muted>Quero usar o app como</Muted>
        <View className="mb-4 mt-2 flex-row gap-2">
          {(['client', 'professional'] as Role[]).map((r) => (
            <Pressable
              key={r}
              onPress={() => toggleRole(r)}
              className={[
                'rounded-full border px-4 py-2',
                roles.includes(r) ? 'border-brand bg-brand' : 'border-neutral-300',
              ].join(' ')}
            >
              <Text className={roles.includes(r) ? 'text-white' : 'text-neutral-700'}>
                {r === 'client' ? 'Cliente' : 'Profissional'}
              </Text>
            </Pressable>
          ))}
        </View>

        {roles.includes('professional') && (
          <Field
            label="Carga máxima (kg)"
            keyboardType="numeric"
            value={form.maxLoadKg}
            onChangeText={set('maxLoadKg')}
          />
        )}

        <ErrorText>{error}</ErrorText>
        <Button
          title="Criar conta"
          onPress={submit}
          loading={loading}
          disabled={roles.length === 0}
        />
      </View>
    </Screen>
  );
}
