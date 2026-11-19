import { describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ApiError } from '../utils/api.js';
import DataTable from './DataTable.jsx';

const ROWS = Array.from({ length: 25 }, (_, i) => ({ id: 'row_' + (i + 1), name: 'Item ' + (i + 1), kind: i % 2 ? 'odd' : 'even', secret: 's' + i }));

function serverFake() {
  return vi.fn(async ({ pageNumber, pageSize, sortBy, sortDirection, filters }) => {
    let rows = ROWS.slice();
    if (filters.kind) rows = rows.filter((r) => r.kind === filters.kind);
    if (filters.name) rows = rows.filter((r) => r.name.includes(filters.name));
    if (sortBy === 'name' && sortDirection === 'desc') rows.reverse();
    const start = (pageNumber - 1) * pageSize;
    return { items: rows.slice(start, start + pageSize), pageNumber, pageSize, totalCount: rows.length, totalPages: Math.ceil(rows.length / pageSize) };
  });
}

const COLUMNS = [
  { key: 'name', header: 'Name', sortKey: 'name', pinned: true, filter: { type: 'text', param: 'name' }, render: (r) => r.name },
  { key: 'kind', header: 'Kind', filter: { type: 'select', param: 'kind', options: [{ value: 'odd', label: 'Odd' }, { value: 'even', label: 'Even' }] }, render: (r) => r.kind },
  { key: 'secret', header: 'Secret', defaultHidden: true, render: (r) => r.secret }
];

function setup(props = {}) {
  const fetchPage = serverFake();
  render(<DataTable tableKey="test" columns={COLUMNS} fetchPage={fetchPage} rowKey={(r) => r.id} defaultPageSize={10} {...props} />);
  return fetchPage;
}

describe('DataTable', () => {
  it('loads the first page from the server and pages forward', async () => {
    const fetchPage = setup();
    expect(await screen.findByText('Item 1')).toBeInTheDocument();
    expect(screen.getByText('1 to 10 of 25')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Next page' }));
    expect(await screen.findByText('Item 11')).toBeInTheDocument();
    expect(fetchPage).toHaveBeenLastCalledWith(expect.objectContaining({ pageNumber: 2, pageSize: 10 }));
  });

  it('sorts on the server when a sortable header is clicked', async () => {
    const fetchPage = setup();
    await screen.findByText('Item 1');
    const header = screen.getByRole('columnheader', { name: 'Name' });
    await userEvent.click(within(header).getByRole('button'));
    await waitFor(() => expect(fetchPage).toHaveBeenLastCalledWith(expect.objectContaining({ sortBy: 'name', sortDirection: 'asc' })));
    await userEvent.click(within(header).getByRole('button'));
    await waitFor(() => expect(fetchPage).toHaveBeenLastCalledWith(expect.objectContaining({ sortBy: 'name', sortDirection: 'desc' })));
    expect(await screen.findByText('Item 25')).toBeInTheDocument();
    expect(header).toHaveAttribute('aria-sort', 'descending');
  });

  it('sends column filters to the server and shows a filtered empty state', async () => {
    const fetchPage = setup();
    await screen.findByText('Item 1');
    await userEvent.selectOptions(screen.getByRole('combobox', { name: 'Filter by Kind' }), 'odd');
    await waitFor(() => expect(fetchPage).toHaveBeenLastCalledWith(expect.objectContaining({ filters: { kind: 'odd' }, pageNumber: 1 })));
    await userEvent.type(screen.getByRole('searchbox', { name: 'Filter by Name' }), 'nothing');
    expect(await screen.findByText('No matches')).toBeInTheDocument();
  });

  it('shows hidden columns from the column chooser and remembers the choice', async () => {
    setup();
    await screen.findByText('Item 1');
    expect(screen.queryByText('s0')).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Columns' }));
    await userEvent.click(screen.getByRole('checkbox', { name: 'Secret' }));
    expect(await screen.findByText('s0')).toBeInTheDocument();
    expect(JSON.parse(window.localStorage.getItem('mymorningnews.table.test.columns'))).toContain('secret');
  });

  it('renders the empty state and the error state', async () => {
    render(<DataTable tableKey="empty" columns={COLUMNS} fetchPage={async () => ({ items: [], totalCount: 0, totalPages: 0 })} rowKey={(r) => r.id} emptyTitle="Nothing yet" />);
    expect(await screen.findByText('Nothing yet')).toBeInTheDocument();
    const failing = vi.fn(async () => {
      throw new ApiError(500, { error: 'InternalError', message: 'boom', requestId: 'req_42' });
    });
    render(<DataTable tableKey="broken" columns={COLUMNS} fetchPage={failing} rowKey={(r) => r.id} />);
    expect(await screen.findByRole('alert')).toHaveTextContent('req_42');
  });

  it('opens rows on click but not when a control inside the row is used', async () => {
    const onRowClick = vi.fn();
    setup({ onRowClick, selectable: true, bulkActions: () => null });
    await userEvent.click(await screen.findByText('Item 1'));
    expect(onRowClick).toHaveBeenCalledWith(expect.objectContaining({ id: 'row_1' }));
    await userEvent.click(screen.getAllByRole('checkbox', { name: 'Select row' })[0]);
    expect(onRowClick).toHaveBeenCalledTimes(1);
    expect(screen.getByText('1 selected')).toBeInTheDocument();
  });
});
