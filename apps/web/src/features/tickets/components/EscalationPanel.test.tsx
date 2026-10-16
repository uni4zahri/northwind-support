import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, expect, it, vi } from 'vitest';
import { ticketsApi } from '../../../api/tickets';
import { ApiError } from '../../../api/client';
import { ticketListItem } from '../../../test/fixtures';
import type { TicketDetail, SlaStatus } from '../../../types/api';
import { EscalationPanel } from './EscalationPanel';
import { SlaCell } from './SlaCell';
vi.mock('../../../api/tickets', () => ({ ticketsApi: { history: vi.fn(), escalate: vi.fn() } }));
const ticket: TicketDetail = { ...ticketListItem(), description: 'Problem', customer: { ...ticketListItem().customer, email: 'a@example.test', phone: null }, category: { ...ticketListItem().category, requiresSpecialist: false } };
beforeEach(() => { vi.clearAllMocks(); vi.mocked(ticketsApi.history).mockResolvedValue([]); });
it('blocks a short reason before calling the API', async () => {
  render(<EscalationPanel ticket={ticket} onUpdated={vi.fn()} />);
  fireEvent.change(screen.getByLabelText(/Reason/), { target: { value: 'bad' } });
  fireEvent.click(screen.getByRole('button', { name: 'Escalate ticket' }));
  expect(await screen.findByRole('alert')).toHaveTextContent('5–500');
  expect(ticketsApi.escalate).not.toHaveBeenCalled();
});
it('displays conflict and leaves the ticket unchanged', async () => {
  vi.mocked(ticketsApi.escalate).mockRejectedValue(new ApiError(409, 'Critical tickets cannot be escalated.'));
  const updated = vi.fn();
  render(<EscalationPanel ticket={ticket} onUpdated={updated} />);
  fireEvent.change(screen.getByLabelText(/Reason/), { target: { value: 'Customer impact increased' } });
  fireEvent.click(screen.getByRole('button', { name: 'Escalate ticket' }));
  await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent('Critical'));
  expect(updated).not.toHaveBeenCalled();
});
it.each<[SlaStatus, string]>([['NotApplicable', 'No SLA'], ['WithinSla', 'Within SLA'], ['AtRisk', 'At risk'], ['Breached', 'Breached'], ['Met', 'Met']])('labels SLA %s without relying on colour', (slaStatus, label) => {
  render(<SlaCell dueAtUtc={null} slaStatus={slaStatus} />);
  expect(screen.getByText(label)).toBeInTheDocument();
});
