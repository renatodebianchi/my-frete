import Constants from 'expo-constants';
import * as Device from 'expo-device';
import * as Notifications from 'expo-notifications';
import { Platform } from 'react-native';

import { apiFetch } from './api/client';

Notifications.setNotificationHandler({
  handleNotification: async () => ({
    shouldShowAlert: true,
    shouldShowBanner: true,
    shouldShowList: true,
    shouldPlaySound: false,
    shouldSetBadge: false,
  }),
});

let lastRegisteredToken: string | null = null;

/**
 * Asks for notification permission and registers the Expo push token with the API
 * (POST /v1/accounts/me/devices). Safe to call repeatedly and on unsupported devices.
 */
export async function registerPushToken(): Promise<void> {
  if (!Device.isDevice) return;

  const settings = await Notifications.getPermissionsAsync();
  let granted = settings.granted;
  if (!granted && settings.canAskAgain) {
    granted = (await Notifications.requestPermissionsAsync()).granted;
  }
  if (!granted) return;

  if (Platform.OS === 'android') {
    await Notifications.setNotificationChannelAsync('default', {
      name: 'Padrão',
      importance: Notifications.AndroidImportance.DEFAULT,
    });
  }

  const projectId =
    Constants.expoConfig?.extra?.eas?.projectId ?? Constants.easConfig?.projectId;

  const { data: token } = await Notifications.getExpoPushTokenAsync(
    projectId ? { projectId } : undefined,
  );

  if (token === lastRegisteredToken) return;

  try {
    await apiFetch<void>('/accounts/me/devices', {
      method: 'POST',
      body: JSON.stringify({ platform: Platform.OS === 'ios' ? 'ios' : 'android', token }),
    });
    lastRegisteredToken = token;
  } catch {
    // best effort — will retry on next app open
  }
}

export async function unregisterPush(): Promise<void> {
  lastRegisteredToken = null;
}
