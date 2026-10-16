import { act, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { request } from '../api/client';
import { Protected } from './Protected';
import { readSession, saveSession } from './session';
const session = { token: 'fixture-token', expiresAtUtc: new Date(Date.now() + 60000).toISOString(), user: { id: 1, fullName: 'Alex', email: 'a@example.test' } };
beforeEach(() => sessionStorage.clear());
afterEach(() => vi.unstubAllGlobals());
it('redirects unauthenticated visitors to login', () => {
  render(<MemoryRouter initialEntries={['/tickets/12']}><Routes><Route path="/tickets/:id" element={<Protected>Private ticket</Protected>} /><Route path="/login" element={<p>Login page</p>} /></Routes></MemoryRouter>);
  expect(screen.getByText('Login page')).toBeInTheDocument();
  expect(screen.queryByText('Private ticket')).not.toBeInTheDocument();
});
it('attaches the bearer token to API requests', async () => {
  saveSession(session);
  const fetcher = vi.fn().mockResolvedValue(new Response('{}', { status: 200 }));
  vi.stubGlobal('fetch', fetcher);
  await request('/tickets');
  expect(fetcher).toHaveBeenCalledWith('/api/tickets', expect.objectContaining({ headers: expect.objectContaining({ Authorization: 'Bearer fixture-token' }) }));
});
it('clears the session and redirects a protected page after a 401', async () => {
  saveSession(session);
  render(<MemoryRouter initialEntries={['/tickets']}><Routes><Route path="/tickets" element={<Protected>Private ticket</Protected>} /><Route path="/login" element={<p>Login page</p>} /></Routes></MemoryRouter>);
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('{"detail":"Expired"}', { status: 401 })));
  await act(async () => { await expect(request('/tickets')).rejects.toMatchObject({ status: 401 }); });
  expect(readSession()).toBeNull();
  expect(screen.getByText('Login page')).toBeInTheDocument();
});
