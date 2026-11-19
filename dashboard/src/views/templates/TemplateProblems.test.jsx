import { describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import TemplateProblems, { problemsFromDetails, sortProblems } from './TemplateProblems.jsx';

const PROBLEMS = [
  { line: 7, message: 'limit must be a whole number from 1 to 25.', isWarning: false },
  { line: 6, message: "Section 'A' has no directive and no instruction.", isWarning: true },
  { line: 3, message: "Unknown option 'foo'.", isWarning: false },
  { line: 3, message: "Unknown option 'foo'.", isWarning: false }
];

describe('TemplateProblems', () => {
  it('summarizes errors and warnings and lists them by line', () => {
    render(<TemplateProblems problems={PROBLEMS} onJump={() => {}} />);
    expect(screen.getByRole('status')).toHaveTextContent('2 errors, 1 warning');
    const items = within(screen.getByRole('list')).getAllByRole('listitem');
    expect(items.map((li) => li.textContent)).toEqual([
      "Line 3ErrorUnknown option 'foo'.",
      "Line 6WarningSection 'A' has no directive and no instruction.",
      'Line 7Errorlimit must be a whole number from 1 to 25.'
    ]);
    expect(items[1]).toHaveClass('warning');
  });

  it('jumps to a line when its number is clicked', async () => {
    const onJump = vi.fn();
    render(<TemplateProblems problems={PROBLEMS} onJump={onJump} />);
    await userEvent.click(screen.getByRole('button', { name: 'Go to line 6' }));
    expect(onJump).toHaveBeenCalledWith(6);
  });

  it('says so when there are no problems, and shows a pending check', () => {
    render(<TemplateProblems problems={[]} checking />);
    expect(screen.getByRole('status')).toHaveTextContent('No problems found.');
    expect(screen.getByText('Checking...')).toBeInTheDocument();
    expect(screen.queryByRole('list')).not.toBeInTheDocument();
  });

  it('reads TemplateInvalid details keyed by line', () => {
    expect(problemsFromDetails({ 'line 3': 'Bad option', 'line 3 (1)': 'Second', other: 'No line' })).toEqual([
      { line: 3, message: 'Bad option', isWarning: false },
      { line: 3, message: 'Second', isWarning: false },
      { line: 0, message: 'No line', isWarning: false }
    ]);
    expect(problemsFromDetails(null)).toEqual([]);
    expect(sortProblems([{ line: 2, message: 'w', isWarning: true }, { line: 2, message: 'e' }]).map((p) => p.message)).toEqual(['e', 'w']);
  });
});
