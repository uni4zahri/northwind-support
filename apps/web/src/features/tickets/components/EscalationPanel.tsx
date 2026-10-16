import { useCallback, useState } from 'react';
import { ticketsApi } from '../../../api/tickets';
import { useAsyncData } from '../../../hooks/useAsyncData';
import type { TicketDetail } from '../../../types/api';
import { formatDateTime } from '../../../lib/format';

export function EscalationPanel({ ticket, onUpdated }: { ticket: TicketDetail; onUpdated: (ticket: TicketDetail) => void }) {
  const [reason, setReason] = useState('');
  const [actor, setActor] = useState('');
  const [pending, setPending] = useState(false);
  const [error, setError] = useState('');
  const load = useCallback(() => ticketsApi.history(ticket.id), [ticket.id]);
  const history = useAsyncData(load, 'Unable to load escalation history.');
  const allowed = ticket.priority !== 'Critical' && !['Resolved', 'Closed'].includes(ticket.status);
  return <section className="card"><h2>Escalation</h2>
    <form onSubmit={async event => {
      event.preventDefault();
      if (reason.trim().length < 5 || reason.trim().length > 500 || !actor.trim()) { setError('Enter a reason of 5–500 characters and your name.'); return; }
      setPending(true); setError('');
      try { const result = await ticketsApi.escalate(ticket.id, reason.trim(), actor.trim()); onUpdated(result.ticket); setReason(''); await history.reload(); }
      catch (e) { setError(e instanceof Error ? e.message : 'Escalation failed.'); }
      finally { setPending(false); }
    }}>
      <label htmlFor="escalation-reason">Reason (5–500 characters)</label>
      <textarea id="escalation-reason" value={reason} onChange={e => setReason(e.target.value)} disabled={!allowed || pending} />
      <label htmlFor="escalation-actor">Your name</label><input id="escalation-actor" value={actor} onChange={e => setActor(e.target.value)} />
      <button className="button" disabled={!allowed || pending}>{pending ? 'Escalating…' : 'Escalate ticket'}</button>
      {!allowed && <p>Critical, Resolved and Closed tickets cannot be escalated.</p>}
      {error && <p role="alert">{error}</p>}
    </form>
    <h3>History</h3>
    {history.isLoading && <p>Loading history…</p>}
    {history.error && <p role="alert">{history.error}</p>}
    {history.data?.length === 0 && <p>No escalations yet.</p>}
    <ol>{history.data?.map(e => <li key={e.id}>
      <strong>{e.fromPriority} → {e.toPriority}</strong>
      <p>Agent: {e.fromAgentId ?? 'Unassigned'} → {e.toAgentId ?? 'Unassigned'}</p>
      <p>Due: {formatDateTime(e.fromDueAtUtc)} → {formatDateTime(e.toDueAtUtc)}</p>
      <p>{e.reason} — {e.escalatedBy}, {formatDateTime(e.escalatedAtUtc)}</p>
    </li>)}</ol>
  </section>;
}
