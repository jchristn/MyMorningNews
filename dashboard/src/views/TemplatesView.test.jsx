import { enumerationResult } from '../test/enumeration.js';
import { describe, expect, it, vi } from 'vitest';
import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ApiError } from '../utils/api.js';
import { MEMBER_DETAILS } from '../test/fakeClient.js';
import { page, renderRoute } from '../test/renderRoute.jsx';
import { EDITION, MARKDOWN, TEMPLATE, TEMPLATE_CONTENT, TEMPLATE_SECTIONS, templateDetail } from '../test/briefingFixtures.js';
import { filterTemplates } from './TemplatesView.jsx';

const MINE = { ...TEMPLATE, id: 'btp_2', name: 'Evening digest', userId: 'usr_admin', isDefault: false, defaultKey: undefined, customized: false, currentVersion: 1 };

function templateApi(overrides = {}) {
  return {
    listBriefingTemplates: vi.fn(async () => page([TEMPLATE, MINE])),
    getBriefingTemplate: vi.fn(async () => templateDetail()),
    listBriefingTemplateVersions: vi.fn(async () => enumerationResult([
      { id: 'btv_1', templateId: 'btp_1', version: 1, content: TEMPLATE_CONTENT, createdUtc: '2026-11-18T06:30:00Z' },
      { id: 'btv_2', templateId: 'btp_1', version: 2, content: TEMPLATE_CONTENT, changeNote: 'Shorter intro', createdUtc: '2026-11-19T06:30:00Z' }
    ])),
    validateBriefingTemplate: vi.fn(async () => ({ valid: true, name: 'Morning News', title: '{{edition.name}}', problems: [], sections: TEMPLATE_SECTIONS, audio: { enabled: false, format: 'narrator', targetMinutes: 8 } })),
    listEditions: vi.fn(async () => page([EDITION])),
    ...overrides
  };
}

describe('TemplatesView list', () => {
  it('lists shared and my templates and filters by scope', async () => {
    renderRoute('/dashboard/templates', templateApi());
    expect(await screen.findByText('Evening digest')).toBeInTheDocument();
    const shared = screen.getByText('Morning News').closest('tr');
    expect(within(shared).getByText('Shared')).toBeInTheDocument();
    expect(within(shared).getByText('Edition default')).toBeInTheDocument();
    expect(within(screen.getByText('Evening digest').closest('tr')).getByText('Mine')).toBeInTheDocument();
    expect(filterTemplates([TEMPLATE, MINE], { scope: 'mine' })).toEqual([MINE]);
    expect(filterTemplates([TEMPLATE, MINE], { scope: 'shared', search: 'morning' })).toEqual([TEMPLATE]);
  });

  it('imports a Markdown file as a new template', async () => {
    const createBriefingTemplate = vi.fn(async () => templateDetail({ template: MINE, shared: false }));
    renderRoute('/dashboard/templates', templateApi({ createBriefingTemplate, getBriefingTemplate: vi.fn(async () => templateDetail({ template: MINE })) }));
    await screen.findByText('Evening digest');
    const file = new File(['---\nname: Weekend\n---\n# {{edition.name}}\n'], 'weekend.md', { type: 'text/markdown' });
    await userEvent.upload(screen.getByTestId('template-import'), file);
    const dialog = await screen.findByRole('dialog', { name: 'Import a template' });
    expect(within(dialog).getByLabelText('Name')).toHaveValue('Weekend');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Import' }));
    await waitFor(() => expect(createBriefingTemplate).toHaveBeenCalledWith({ content: '---\nname: Weekend\n---\n# {{edition.name}}\n', name: 'Weekend', shared: false }));
  });
});

describe('TemplateEditor', () => {
  it('loads the template with its outline and versions', async () => {
    renderRoute('/dashboard/templates/btp_1', templateApi());
    const editor = await screen.findByLabelText('Template', { selector: 'textarea' });
    expect(editor.value).toBe(TEMPLATE_CONTENT);
    expect(screen.getByText('No problems found.')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('tab', { name: 'Outline' }));
    const outline = screen.getByTestId('template-outline');
    expect(within(outline).getByText('tier=MustRead')).toBeInTheDocument();
    expect(within(outline).getByText('source=citations')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('tab', { name: 'Versions' }));
    expect(screen.getByText('Shorter intro')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Roll back to v1' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Reset to default/ })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^Delete$/ })).not.toBeInTheDocument();
  });

  it('validates the draft live and shows problems by line', async () => {
    const validateBriefingTemplate = vi.fn(async () => ({
      valid: false,
      name: 'Morning News',
      title: '{{nope}}',
      problems: [{ line: 6, message: 'Unknown variable {{nope}}.', isWarning: false }],
      sections: [],
      audio: { enabled: false }
    }));
    renderRoute('/dashboard/templates/btp_1', templateApi({ validateBriefingTemplate }));
    const editor = await screen.findByLabelText('Template', { selector: 'textarea' });
    fireEvent.change(editor, { target: { value: TEMPLATE_CONTENT.replace('{{edition.name}}', '{{nope}}') } });
    expect(await screen.findByText('Unknown variable {{nope}}.', {}, { timeout: 2000 })).toBeInTheDocument();
    expect(validateBriefingTemplate).toHaveBeenCalledTimes(1);
    expect(screen.getByRole('tab', { name: 'Problems (1)' })).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Go to line 6' }));
    expect(editor.selectionStart).toBe(TEMPLATE_CONTENT.split('\n').slice(0, 5).join('\n').length + 1);
  });

  it('shows the server problems when a save is rejected', async () => {
    const saveBriefingTemplate = vi.fn(async () => {
      throw new ApiError(400, { error: 'TemplateInvalid', message: 'The template has problems.', details: { 'line 9': 'limit must be a whole number from 1 to 25.' } });
    });
    renderRoute('/dashboard/templates/btp_1', templateApi({ saveBriefingTemplate }));
    const editor = await screen.findByLabelText('Template', { selector: 'textarea' });
    fireEvent.change(editor, { target: { value: TEMPLATE_CONTENT.replace('limit=5', 'limit=x') } });
    await userEvent.type(screen.getByLabelText('Change note'), 'Try x');
    await userEvent.click(screen.getByRole('button', { name: 'Save version' }));
    expect(await screen.findByText('The server rejected this version.')).toBeInTheDocument();
    expect(screen.getByText('limit must be a whole number from 1 to 25.')).toBeInTheDocument();
    expect(saveBriefingTemplate).toHaveBeenCalledWith('btp_1', TEMPLATE_CONTENT.replace('limit=5', 'limit=x'), 'Try x');
  });

  it('previews an edition that uses the template', async () => {
    const previewEdition = vi.fn(async () => ({ markdown: MARKDOWN }));
    renderRoute('/dashboard/templates/btp_1', templateApi({ previewEdition }));
    await screen.findByLabelText('Template', { selector: 'textarea' });
    await userEvent.click(screen.getByRole('tab', { name: 'Preview' }));
    await userEvent.click(await screen.findByRole('button', { name: /Preview/ }));
    expect(await screen.findByTestId('template-preview')).toHaveTextContent('Coding agents reached general availability.');
    expect(previewEdition).toHaveBeenCalledWith('edn_1', expect.anything());
    expect(screen.queryByText(/uses a different template/)).not.toBeInTheDocument();
  });

  it('is read-only for members on shared templates', async () => {
    renderRoute('/dashboard/templates/btp_1', templateApi({ getTokenDetails: vi.fn(async () => MEMBER_DETAILS) }));
    const editor = await screen.findByLabelText('Template', { selector: 'textarea' });
    expect(editor).toHaveAttribute('readonly');
    expect(screen.getByText(/Only tenant administrators can change shared templates/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Save version' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Reset to default/ })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Make a copy/ })).toBeInTheDocument();
  });
});
