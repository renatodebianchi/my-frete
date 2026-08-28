import './src/lib/global.css';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { StatusBar } from 'expo-status-bar';
import { GestureHandlerRootView } from 'react-native-gesture-handler';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import { Text, View } from 'react-native';

import { config } from '@/lib/config';

const queryClient = new QueryClient();

export default function App() {
  return (
    <GestureHandlerRootView style={{ flex: 1 }}>
      <SafeAreaProvider>
        <QueryClientProvider client={queryClient}>
          {/* Navigation stacks (auth / client / pro) land in Phase 2 / T023. */}
          <View className="flex-1 items-center justify-center bg-white">
            <Text className="text-xl font-semibold text-brand">my-frete</Text>
            <Text className="mt-2 text-neutral-500">API: {config.apiBaseUrl}</Text>
          </View>
          <StatusBar style="auto" />
        </QueryClientProvider>
      </SafeAreaProvider>
    </GestureHandlerRootView>
  );
}
