import { describe, expect, it, vi } from 'vitest';
import { csvField, downloadText, toCsv, USAGE_CSV_COLUMNS, usageCsvFilename, usageReportCsv } from './csv.js';

describe('csvField', () => {
  it('quotes commas, quotes, and line breaks', () => {
    expect(csvField('plain')).toBe('plain');
    expect(csvField('a,b')).toBe('"a,b"');
    expect(csvField('say "hi"')).toBe('"say ""hi"""');
    expect(csvField('two\nlines')).toBe('"two\nlines"');
    expect(csvField(null)).toBe('');
    expect(csvField(undefined)).toBe('');
    expect(csvField(0)).toBe('0');
  });

  it('neutralizes spreadsheet formulas but keeps negative numbers', () => {
    expect(csvField('=SUM(A1)')).toBe("'=SUM(A1)");
    expect(csvField('@cmd')).toBe("'@cmd");
    expect(csvField('-1.5')).toBe('-1.5');
    expect(csvField(-2)).toBe('-2');
  });
});

describe('toCsv', () => {
  it('writes a header row and CRLF line endings', () => {
    const text = toCsv([{ a: 1, b: 'x,y' }], [
      { key: 'a', header: 'A' },
      { key: 'b', header: 'B' },
      { key: 'c', header: 'C', value: (r) => r.a * 2 }
    ]);
    expect(text).toBe('A,B,C\r\n1,"x,y",2\r\n');
  });
});

describe('usageReportCsv', () => {
  it('exports every row plus a total row in the documented column order', () => {
    const report = {
      groupBy: 'User',
      rows: [{ key: 'usr_1', label: 'a@b.c', calls: 3, costUsd: 0.5, costShare: 1 }],
      totals: { key: 'total', calls: 3, costUsd: 0.5, costShare: 0 }
    };
    const lines = usageReportCsv(report).trim().split('\r\n');
    expect(lines[0]).toBe(USAGE_CSV_COLUMNS.map((c) => c.header).join(','));
    expect(lines).toHaveLength(3);
    expect(lines[1].startsWith('usr_1,a@b.c,3,')).toBe(true);
    expect(lines[2].startsWith('total,Total,3,')).toBe(true);
    expect(usageReportCsv(null).trim().split('\r\n')).toHaveLength(1);
  });

  it('names the file after the grouping and the window', () => {
    expect(usageCsvFilename('usage', 'User', '2026-10-01T00:00:00Z', '2026-11-01T00:00:00Z')).toBe('usage-user-2026-10-01-2026-11-01.csv');
  });
});

describe('downloadText', () => {
  it('clicks a temporary link to a blob URL', () => {
    const create = vi.fn(() => 'blob:x');
    const revoke = vi.fn();
    const original = { create: URL.createObjectURL, revoke: URL.revokeObjectURL };
    URL.createObjectURL = create;
    URL.revokeObjectURL = revoke;
    const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});
    downloadText('a.csv', 'x');
    expect(create).toHaveBeenCalledTimes(1);
    expect(click).toHaveBeenCalledTimes(1);
    click.mockRestore();
    URL.createObjectURL = original.create;
    URL.revokeObjectURL = original.revoke;
  });
});
