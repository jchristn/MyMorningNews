import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ApiError } from '../utils/api.js';
import ConfirmModal from './ConfirmModal.jsx';
import CopyButton from './CopyButton.jsx';
import EmptyState from './EmptyState.jsx';
import ErrorState from './ErrorState.jsx';

describe('EmptyState', () => {
  it('renders the title, message, and action', () => {
    render(<EmptyState title="No topics" message="Create one to start." action={<button type="button">Create</button>} />);
    expect(screen.getByRole('status')).toHaveTextContent('No topics');
    expect(screen.getByText('Create one to start.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Create' })).toBeInTheDocument();
  });
});

describe('ErrorState', () => {
  it('renders nothing without an error', () => {
    const { container } = render(<ErrorState error={null} />);
    expect(container).toBeEmptyDOMElement();
  });

  it('shows the request ID and retries', async () => {
    const onRetry = vi.fn();
    render(<ErrorState error={new ApiError(500, { error: 'InternalError', message: 'Kaboom', requestId: 'req_7' })} onRetry={onRetry} />);
    expect(screen.getByRole('alert')).toHaveTextContent('req_7');
    await userEvent.click(screen.getByRole('button', { name: 'Retry' }));
    expect(onRetry).toHaveBeenCalled();
  });

  it('explains network failures', () => {
    render(<ErrorState error={new ApiError(0, null, 'Failed to fetch')} />);
    expect(screen.getByRole('alert')).toHaveTextContent('could not be reached');
  });

  it('renders a dismissible inline banner', async () => {
    const onDismiss = vi.fn();
    render(<ErrorState error={new ApiError(403, { error: 'Forbidden' })} inline onDismiss={onDismiss} />);
    await userEvent.click(screen.getByRole('button', { name: 'Dismiss' }));
    expect(onDismiss).toHaveBeenCalled();
  });
});

describe('ConfirmModal', () => {
  it('runs the async action and closes', async () => {
    const onConfirm = vi.fn(async () => {});
    const onClose = vi.fn();
    render(<ConfirmModal open title="Delete tenant" message="Sure?" confirmLabel="Delete" danger onConfirm={onConfirm} onClose={onClose} />);
    expect(screen.getByRole('dialog', { name: 'Delete tenant' })).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Delete' }));
    expect(onConfirm).toHaveBeenCalled();
    expect(onClose).toHaveBeenCalled();
  });

  it('keeps the dialog open and shows the error when the action fails', async () => {
    const onClose = vi.fn();
    const onConfirm = async () => {
      throw new ApiError(409, { error: 'Conflict', message: 'In use', requestId: 'req_c' });
    };
    render(<ConfirmModal open title="Delete role" message="Sure?" confirmLabel="Delete" onConfirm={onConfirm} onClose={onClose} />);
    await userEvent.click(screen.getByRole('button', { name: 'Delete' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('req_c');
    expect(onClose).not.toHaveBeenCalled();
  });

  it('closes on Escape', async () => {
    const onClose = vi.fn();
    render(<ConfirmModal open title="Leave" message="Sure?" onConfirm={async () => {}} onClose={onClose} />);
    await userEvent.keyboard('{Escape}');
    expect(onClose).toHaveBeenCalled();
  });
});

describe('CopyButton', () => {
  it('copies the value and confirms', async () => {
    const user = userEvent.setup();
    render(<CopyButton value="abc" />);
    await user.click(screen.getByRole('button', { name: 'Copy' }));
    expect(await navigator.clipboard.readText()).toBe('abc');
    expect(screen.getByRole('button', { name: 'Copied' })).toBeInTheDocument();
  });
});
