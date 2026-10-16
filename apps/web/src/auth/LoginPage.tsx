import { useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { request } from '../api/client';
import { saveSession, type Session } from './session';
export function LoginPage() {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [pending, setPending] = useState(false);
  const [error, setError] = useState('');
  const navigate = useNavigate();
  const location = useLocation();
  return <main className="app__main"><section className="card"><h1>Sign in to Northwind Support</h1>
    <form onSubmit={async event => {
      event.preventDefault();
      if (!email.trim() || !password) { setError('Email and password are required.'); return; }
      setPending(true); setError('');
      try {
        const session = await request<Session>('/auth/login', { method: 'POST', body: JSON.stringify({ email: email.trim(), password }) });
        saveSession(session);
        const state = location.state as { from?: string } | null;
        const from = state?.from;
        await navigate(from?.startsWith('/') && !from.startsWith('//') && from !== '/login' ? from : '/tickets', { replace: true });
      } catch (e) { setError(e instanceof Error ? e.message : 'Sign-in failed.'); }
      finally { setPending(false); }
    }}>
      <div className="field"><label htmlFor="login-email">Email</label><input id="login-email" type="email" autoComplete="username" required maxLength={256} value={email} onChange={e => setEmail(e.target.value)} disabled={pending} /></div>
      <div className="field"><label htmlFor="login-password">Password</label><input id="login-password" type="password" autoComplete="current-password" required maxLength={1024} value={password} onChange={e => setPassword(e.target.value)} disabled={pending} /></div>
      <button className="button button--primary" disabled={pending}>{pending ? 'Signing in…' : 'Sign in'}</button>
      {error && <p role="alert">{error}</p>}
    </form>
  </section></main>;
}
