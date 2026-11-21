// Zapis pliku pobranego z API (raport, kopia dokumentu) w przeglądarce: tymczasowy adres obiektu i kliknięcie łącza.
export function saveBlob(blob: Blob, fileName: string): void {
  if (typeof URL.createObjectURL !== 'function') return;
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  link.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
