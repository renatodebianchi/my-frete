import type { ReactNode } from 'react';
import {
  ActivityIndicator,
  Pressable,
  Text,
  TextInput,
  View,
  type TextInputProps,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

export function Screen({ children }: { children: ReactNode }) {
  return (
    <SafeAreaView className="flex-1 bg-white">
      <View className="flex-1 px-6 py-4">{children}</View>
    </SafeAreaView>
  );
}

export function Heading({ children }: { children: ReactNode }) {
  return <Text className="text-2xl font-semibold text-neutral-900">{children}</Text>;
}

export function Muted({ children }: { children: ReactNode }) {
  return <Text className="text-base text-neutral-500">{children}</Text>;
}

export function Field({ label, ...props }: TextInputProps & { label: string }) {
  return (
    <View className="mb-4">
      <Text className="mb-1 text-sm font-medium text-neutral-700">{label}</Text>
      <TextInput
        className="rounded-lg border border-neutral-300 px-3 py-3 text-base text-neutral-900"
        placeholderTextColor="#9CA3AF"
        {...props}
      />
    </View>
  );
}

export function Button({
  title,
  onPress,
  loading,
  variant = 'primary',
  disabled,
}: {
  title: string;
  onPress: () => void;
  loading?: boolean;
  variant?: 'primary' | 'ghost';
  disabled?: boolean;
}) {
  const isPrimary = variant === 'primary';
  return (
    <Pressable
      accessibilityRole="button"
      disabled={disabled || loading}
      onPress={onPress}
      className={[
        'items-center rounded-lg px-4 py-3',
        isPrimary ? 'bg-brand' : 'bg-transparent',
        disabled || loading ? 'opacity-50' : '',
      ].join(' ')}
    >
      {loading ? (
        <ActivityIndicator color={isPrimary ? '#fff' : '#0F766E'} />
      ) : (
        <Text className={isPrimary ? 'text-base font-semibold text-white' : 'text-base font-semibold text-brand'}>
          {title}
        </Text>
      )}
    </Pressable>
  );
}

export function ErrorText({ children }: { children: ReactNode }) {
  return children ? <Text className="mb-3 text-sm text-red-600">{children}</Text> : null;
}
