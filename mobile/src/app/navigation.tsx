import { NavigationContainer } from '@react-navigation/native';
import { createNativeStackNavigator } from '@react-navigation/native-stack';
import { useEffect } from 'react';
import { ActivityIndicator, Pressable, Text, View } from 'react-native';

import { useAuthStore } from '@/features/auth/store';

import { WelcomeScreen } from './auth/WelcomeScreen';
import { LoginScreen } from './auth/LoginScreen';
import { RegisterScreen } from './auth/RegisterScreen';
import { ClientHomeScreen } from './client/ClientHomeScreen';
import { ProHomeScreen } from './pro/ProHomeScreen';

export type AuthStackParamList = { Welcome: undefined; Login: undefined; Register: undefined };
export type AppStackParamList = { ClientHome: undefined; ProHome: undefined };

const AuthStack = createNativeStackNavigator<AuthStackParamList>();
const AppStack = createNativeStackNavigator<AppStackParamList>();

function Splash() {
  return (
    <View className="flex-1 items-center justify-center bg-white">
      <ActivityIndicator color="#0F766E" />
    </View>
  );
}

function AuthNavigator() {
  return (
    <AuthStack.Navigator screenOptions={{ headerShadowVisible: false, headerTitle: '' }}>
      <AuthStack.Screen name="Welcome" component={WelcomeScreen} options={{ headerShown: false }} />
      <AuthStack.Screen name="Login" component={LoginScreen} />
      <AuthStack.Screen name="Register" component={RegisterScreen} />
    </AuthStack.Navigator>
  );
}

function AppNavigator() {
  const roles = useAuthStore((s) => s.user?.roles ?? []);
  const both = roles.includes('client') && roles.includes('professional');
  const initial = roles.includes('client') ? 'ClientHome' : 'ProHome';

  return (
    <AppStack.Navigator
      initialRouteName={initial}
      screenOptions={({ navigation, route }) => ({
        headerShadowVisible: false,
        headerRight: both
          ? () => (
              <Pressable
                onPress={() =>
                  navigation.navigate(route.name === 'ClientHome' ? 'ProHome' : 'ClientHome')
                }
              >
                <Text className="text-brand">
                  {route.name === 'ClientHome' ? 'Modo profissional' : 'Modo cliente'}
                </Text>
              </Pressable>
            )
          : undefined,
      })}
    >
      <AppStack.Screen name="ClientHome" component={ClientHomeScreen} options={{ title: 'Início' }} />
      <AppStack.Screen name="ProHome" component={ProHomeScreen} options={{ title: 'Profissional' }} />
    </AppStack.Navigator>
  );
}

export function RootNavigator() {
  const status = useAuthStore((s) => s.status);
  const bootstrap = useAuthStore((s) => s.bootstrap);

  useEffect(() => {
    void bootstrap();
  }, [bootstrap]);

  return (
    <NavigationContainer>
      {status === 'loading' ? <Splash /> : status === 'signedIn' ? <AppNavigator /> : <AuthNavigator />}
    </NavigationContainer>
  );
}
