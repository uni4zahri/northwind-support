import { useSyncExternalStore } from 'react';
export interface Session { token: string; expiresAtUtc: string; user: { id: number; fullName: string; email: string } }
const key = 'northwind-session';
const event = 'northwind-session-changed';
export function readSession(): Session | null {
  try {
    const raw = sessionStorage.getItem(key);
    const value: Session | null = raw ? JSON.parse(raw) as Session : null;
    return value?.token && value.user?.fullName && Date.parse(value.expiresAtUtc) > Date.now() ? value : null;
  } catch { return null; }
}
export function saveSession(value: Session | null) {
  if (value) sessionStorage.setItem(key, JSON.stringify(value)); else sessionStorage.removeItem(key);
  window.dispatchEvent(new Event(event));
}
function subscribe(listener: () => void) {
  window.addEventListener(event, listener);
  window.addEventListener('storage', listener);
  return () => { window.removeEventListener(event, listener); window.removeEventListener('storage', listener); };
}
export function useSession() {
  useSyncExternalStore(subscribe, () => sessionStorage.getItem(key), () => null);
  return readSession();
}
