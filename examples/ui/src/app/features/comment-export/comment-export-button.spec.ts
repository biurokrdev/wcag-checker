import { TestBed } from '@angular/core/testing';
import { CommentExportButton } from './comment-export-button';

describe('CommentExportButton', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [CommentExportButton] }).compileComponents();
  });

  it('takes the report id and file name and renders nothing yet', () => {
    const fixture = TestBed.createComponent(CommentExportButton);
    fixture.componentRef.setInput('reportId', 'r1');
    fixture.componentRef.setInput('fileName', 'pismo.docx');
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('button')).toBeNull();
    expect(el.textContent!.trim()).toBe('');
  });
});
