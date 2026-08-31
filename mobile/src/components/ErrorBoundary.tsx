import { Component, type ErrorInfo, type ReactNode } from 'react';
import { Text, View } from 'react-native';

import { Button } from './ui';

type Props = { children: ReactNode };
type State = { error: Error | null };

export class ErrorBoundary extends Component<Props, State> {
  state: State = { error: null };

  static getDerivedStateFromError(error: Error): State {
    return { error };
  }

  componentDidCatch(error: Error, info: ErrorInfo): void {
    // eslint-disable-next-line no-console
    console.error('Unhandled UI error', error, info.componentStack);
  }

  render(): ReactNode {
    if (!this.state.error) return this.props.children;

    return (
      <View className="flex-1 items-center justify-center bg-white px-6">
        <Text className="mb-2 text-lg font-semibold text-neutral-900">Algo deu errado</Text>
        <Text className="mb-6 text-center text-neutral-500">
          Ocorreu um erro inesperado. Tente novamente.
        </Text>
        <Button title="Tentar de novo" onPress={() => this.setState({ error: null })} />
      </View>
    );
  }
}
