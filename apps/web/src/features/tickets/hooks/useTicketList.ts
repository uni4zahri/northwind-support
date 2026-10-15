import { useEffect, useState } from 'react';
import { ApiError } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import type { PagedResult, TicketFilters, TicketListItem } from '../../../types/api';

const DEBOUNCE_MS = 300;

export const emptyFilters: TicketFilters = {
  search: '',
  status: undefined,
  priority: undefined,
  categoryId: undefined,
  customerId: undefined,
  assignedAgentId: undefined,
  unassignedOnly: false,
  page: 1,
  pageSize: 20,
  sortBy: 'createdAtUtc',
  sortDirection: 'desc',
};

interface State {
  isLoading: boolean;
  data?: PagedResult<TicketListItem>;
  error?: string;
}

/** Owns the ticket list's filters and the data they produce. */
export function useTicketList() {
  const [filters, setFilters] = useState<TicketFilters>(emptyFilters);
  const [state, setState] = useState<State>({ isLoading: true });

  const [search, setSearch] = useState(filters.search);
  const [refresh, setRefresh] = useState(0);
  useEffect(() => {
    const handle = window.setTimeout(() => setSearch(filters.search), DEBOUNCE_MS);
    return () => window.clearTimeout(handle);
  }, [filters.search]);

  // A value-based key includes future filters automatically; only search is delayed.
  const queryKey = JSON.stringify({ ...filters, search });
  useEffect(() => {
    let cancelled = false;
    const query: TicketFilters = JSON.parse(queryKey);
    const load = async () => {
      setState((current) => ({ ...current, isLoading: true, error: undefined }));
      try {
        const data = await ticketsApi.getTickets(query);
        if (!cancelled) setState({ isLoading: false, data });
      } catch (error) {
        const message = error instanceof ApiError ? error.message : 'Unable to load tickets.';
        if (!cancelled) setState((current) => ({ ...current, isLoading: false, error: message }));
      }
    };
    void load();
    return () => { cancelled = true; };
  }, [queryKey, refresh]);

  const updateFilters = (patch: Partial<TicketFilters>) =>
    setFilters((current) => ({ ...current, ...patch, page: patch.page ?? 1 }));

  const resetFilters = () => setFilters(emptyFilters);

  return { filters, updateFilters, resetFilters, reload: () => setRefresh((value) => value + 1), ...state };
}
