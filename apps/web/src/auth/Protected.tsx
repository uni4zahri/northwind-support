import { Navigate, useLocation } from 'react-router-dom';
import type { ReactNode } from 'react';
import { useSession } from './session';
export function Protected({ children }: { children: ReactNode }) {
  const session = useSession();
  const location = useLocation();
  return session ? children : <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />;
}
