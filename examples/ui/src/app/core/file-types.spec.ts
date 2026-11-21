import { DEFAULT_FILE_TYPE, FILE_SIZE_LIMIT_BYTES, FILE_TYPES, checkFile, fileTypeConfig } from './file-types';
import { MAX_FILE_SIZE_BYTES } from './models';

const MB = 1024 * 1024;
const file = (name: string, size = 10) => ({ name, size });

describe('FILE_TYPES', () => {
  it('lists Word, Excel and PDF in this order with Word as the default', () => {
    expect(FILE_TYPES.map((t) => t.id)).toEqual(['docx', 'xlsx', 'pdf']);
    expect(FILE_TYPES.map((t) => t.label)).toEqual(['Word', 'Excel', 'PDF']);
    expect(DEFAULT_FILE_TYPE).toBe('docx');
    expect(fileTypeConfig(DEFAULT_FILE_TYPE).enabled).toBe(true);
  });

  it('keeps the Word texts and accept filter exactly as before the file type picker', () => {
    const docx = fileTypeConfig('docx');
    expect(docx.uploadTitle).toBe('Plik szablonu (*.docx)');
    expect(docx.uploadHint).toBe('Dopuszczalny format pliku: DOCX, do 20 MB');
    expect(docx.accept).toBe('.docx');
    expect(docx.mime).toEqual(['application/vnd.openxmlformats-officedocument.wordprocessingml.document']);
    expect(docx.standards).toEqual({ kind: 'api-catalog' });
  });

  it('describes Excel with its own texts, an accept filter with the extension and MIME type, and WCAG only', () => {
    const xlsx = fileTypeConfig('xlsx');
    expect(xlsx.enabled).toBe(true);
    expect(xlsx.uploadTitle).toBe('Plik arkusza (*.xlsx)');
    expect(xlsx.uploadHint).toBe('Dopuszczalny format pliku: XLSX (Excel), do 20 MB');
    expect(xlsx.accept).toBe('.xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet');
    expect(xlsx.standards).toEqual({
      kind: 'fixed',
      items: [{ key: 'wcag-xlsx', name: 'WCAG (arkusz Excel)', required: true }],
      versionLegend: 'Wersja WCAG',
      versions: [
        { id: '2.2', label: 'WCAG 2.2 AA' },
        { id: '2.1', label: 'WCAG 2.1 AA' },
      ],
      defaultVersion: '2.2',
    });
  });

  it('marks PDF as disabled and coming soon, without standards', () => {
    const pdf = fileTypeConfig('pdf');
    expect(pdf.enabled).toBe(false);
    expect(pdf.comingSoon).toBe(true);
    expect(pdf.standards).toEqual({ kind: 'none' });
  });

  it('gives every type a unique id, a dotted lower-case extension and a MIME type', () => {
    expect(new Set(FILE_TYPES.map((t) => t.id)).size).toBe(FILE_TYPES.length);
    for (const type of FILE_TYPES) {
      expect(type.extensions.length).toBeGreaterThan(0);
      for (const ext of type.extensions) expect(ext).toMatch(/^\.[a-z0-9]+$/);
      expect(type.mime.length).toBeGreaterThan(0);
      expect(type.accept.split(',')[0]).toBe(type.extensions[0]);
    }
  });

  it('shares one size limit taken from the existing constant', () => {
    expect(FILE_SIZE_LIMIT_BYTES).toBe(MAX_FILE_SIZE_BYTES);
    expect(FILE_SIZE_LIMIT_BYTES).toBe(20 * MB);
  });
});

describe('checkFile', () => {
  const docx = fileTypeConfig('docx');
  const xlsx = fileTypeConfig('xlsx');

  it('accepts a file with the extension of the chosen type, ignoring letter case', () => {
    expect(checkFile(file('pismo.docx'), docx)).toEqual({ ok: true });
    expect(checkFile(file('PISMO.DOCX'), docx)).toEqual({ ok: true });
    expect(checkFile(file('budżet.xlsx'), xlsx)).toEqual({ ok: true });
    expect(checkFile(file('Budżet.XLSX'), xlsx)).toEqual({ ok: true });
  });

  it('accepts a file exactly at the size limit and rejects one byte more', () => {
    expect(checkFile(file('a.xlsx', FILE_SIZE_LIMIT_BYTES), xlsx)).toEqual({ ok: true });
    expect(checkFile(file('a.xlsx', FILE_SIZE_LIMIT_BYTES + 1), xlsx)).toEqual({
      ok: false,
      reason: 'too-large',
      message: 'Plik jest za duży. Dopuszczalny rozmiar to 20 MB.',
    });
  });

  it('takes the size limit from the options', () => {
    expect(checkFile(file('a.xlsx', 2 * MB + 1), xlsx, { maxBytes: 2 * MB })).toEqual({
      ok: false,
      reason: 'too-large',
      message: 'Plik jest za duży. Dopuszczalny rozmiar to 2 MB.',
    });
  });

  it('rejects an empty file', () => {
    expect(checkFile(file('a.xlsx', 0), xlsx)).toEqual({ ok: false, reason: 'empty', message: 'Plik jest pusty.' });
    expect(checkFile(file('a.docx', 0), docx)).toEqual({ ok: false, reason: 'empty', message: 'Plik jest pusty.' });
  });

  it('keeps the Word rejection texts unchanged, also for files of another supported type', () => {
    expect(checkFile(file('notatka.txt'), docx)).toEqual({ ok: false, reason: 'wrong-format', message: '„notatka.txt” nie jest plikiem DOCX.' });
    expect(checkFile(file('arkusz.xlsx'), docx)).toEqual({ ok: false, reason: 'wrong-format', message: '„arkusz.xlsx” nie jest plikiem DOCX.' });
    expect(checkFile(file('skan.pdf'), docx)).toEqual({ ok: false, reason: 'wrong-format', message: '„skan.pdf” nie jest plikiem DOCX.' });
  });

  it('names the other supported type and suggests switching to it when Excel gets a Word document', () => {
    expect(checkFile(file('raport.docx'), xlsx)).toEqual({
      ok: false,
      reason: 'other-type',
      message: 'Wybrano typ pliku Excel, a dodany plik to dokument Word (.docx). Dodaj plik .xlsx albo zmień typ pliku na Word.',
    });
  });

  it('does not suggest switching to a type that is not available yet', () => {
    expect(checkFile(file('skan.PDF'), xlsx)).toEqual({
      ok: false,
      reason: 'other-type',
      message: 'Wybrano typ pliku Excel, a dodany plik to dokument PDF (.pdf). Dodaj plik .xlsx.',
    });
  });

  it('rejects an unknown extension, a legacy spreadsheet and a file without extension for Excel', () => {
    expect(checkFile(file('dane.csv'), xlsx)).toEqual({ ok: false, reason: 'wrong-format', message: '„dane.csv” nie jest plikiem XLSX.' });
    expect(checkFile(file('stary.xls'), xlsx)).toEqual({ ok: false, reason: 'wrong-format', message: '„stary.xls” nie jest plikiem XLSX.' });
    expect(checkFile(file('xlsx'), xlsx)).toEqual({ ok: false, reason: 'wrong-format', message: '„xlsx” nie jest plikiem XLSX.' });
  });

  it('checks the extension before the size', () => {
    expect(checkFile(file('raport.docx', 0), xlsx)).toMatchObject({ ok: false, reason: 'other-type' });
  });
});
