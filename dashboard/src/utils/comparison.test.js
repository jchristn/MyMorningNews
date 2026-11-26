import { describe, expect, it } from 'vitest';
import { clampBudget, clampCount, deltaHref, groupDeltas, historyForm, historyPreviewBody, isComparison, parseMode, previewBlocker, sinceIso } from './comparison.js';

describe('comparison helpers', () => {
  it('builds the preview body from unsaved form values', () => {
    const form = historyForm({ historyMode: 'SinceDate', historyCount: 4, historySinceUtc: '2026-11-01T00:00:00Z', historyTokenBudget: 2000 });
    expect(form).toEqual({ historyMode: 'SinceDate', historyCount: 4, historySinceUtc: '2026-11-01', historyBriefingId: '', historyTokenBudget: 2000 });
    expect(historyPreviewBody(form)).toEqual({ historyMode: 'SinceDate', historyCount: 4, historySinceUtc: '2026-11-01T00:00:00.000Z', historyBriefingId: null, historyTokenBudget: 2000 });
    expect(historyPreviewBody({ ...form, historyMode: 'SpecificIssue', historyBriefingId: '' })).toMatchObject({ historySinceUtc: null, historyBriefingId: '' });
    expect(historyForm(null)).toMatchObject({ historyMode: 'Off', historyCount: 5, historyTokenBudget: 8000 });
  });

  it('blocks previews that need a date or an issue', () => {
    expect(previewBlocker({ historyMode: 'SinceDate', historySinceUtc: '' })).toBe('date');
    expect(previewBlocker({ historyMode: 'SpecificIssue', historyBriefingId: '' })).toBe('issue');
    expect(previewBlocker({ historyMode: 'PreviousIssue' })).toBeNull();
    expect(sinceIso('not a date')).toBeNull();
  });

  it('clamps the count and budget to the server bounds', () => {
    expect(clampBudget(100)).toBe(256);
    expect(clampBudget(99999)).toBe(32000);
    expect(clampBudget('')).toBe(8000);
    expect(clampCount(0)).toBe(1);
    expect(clampCount(50)).toBe(20);
  });

  it('parses edition modes, groups deltas in display order, and links evidence', () => {
    expect(parseMode('Edition:PreviousIssue')).toEqual({ mode: 'PreviousIssue', edition: true });
    expect(parseMode(null)).toEqual({ mode: 'Off', edition: false });
    const groups = groupDeltas([
      { id: 'a', kind: 'Unchanged', sortOrder: 3 },
      { id: 'b', kind: 'NewDevelopment', sortOrder: 2 },
      { id: 'c', kind: 'Correction', sortOrder: 0 },
      { id: 'd', kind: 'Mystery', sortOrder: 4 }
    ]);
    expect(groups.map((g) => g.kind)).toEqual(['Correction', 'NewDevelopment', 'Unchanged', 'Mystery']);
    expect(deltaHref({ findingId: 'fnd_1' })).toBe('/dashboard/findings/fnd_1');
    expect(deltaHref({ predictionId: 'prd_1' })).toBe('/dashboard/predictions?id=prd_1');
    expect(deltaHref({})).toBeNull();
    expect(isComparison({ compared: false })).toBe(true);
    expect(isComparison({ objects: [] })).toBe(false);
  });
});
