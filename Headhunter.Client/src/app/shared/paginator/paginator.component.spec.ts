import { TestBed } from '@angular/core/testing';
import { votersPage, voterRow } from '../../../testing/synthetic-data';
import { PaginatorComponent } from './paginator.component';

describe('PaginatorComponent', () => {
  async function render(pageNumber: number, pageSize: number, totalCount: number, rowsOnPage: number) {
    const fixture = TestBed.createComponent(PaginatorComponent);
    const rows = Array.from({ length: rowsOnPage }, (_, i) => voterRow({ id: `voter-${i + 1}` }));
    fixture.componentRef.setInput('paginationResult', votersPage(rows, totalCount));
    fixture.componentRef.setInput('pageNumber', pageNumber);
    fixture.componentRef.setInput('pageSize', pageSize);
    fixture.detectChanges();
    await fixture.whenStable(); // <option [value]> bindings settle after the select first writes its value
    const el = fixture.nativeElement as HTMLElement;
    const [prev, next] = Array.from(el.querySelectorAll('x-button button')) as HTMLButtonElement[];
    const texts = () => Array.from(el.querySelectorAll('p')).map(p => p.textContent!.trim());
    return { fixture, component: fixture.componentInstance, el, prev, next, texts };
  }

  it('describes the current page and item range', async () => {
    const { texts } = await render(2, 10, 25, 10);
    expect(texts()).toEqual(['11–20 of 25', 'Page 2 of 3']);
  });

  it('uses the actual row count for the end of the last page', async () => {
    const { texts } = await render(3, 10, 25, 5);
    expect(texts()).toEqual(['21–25 of 25', 'Page 3 of 3']);
  });

  it('writes big counts with thousands separators', async () => {
    const { texts } = await render(1, 10, 8_506_687, 10);
    expect(texts()).toEqual(['1–10 of 8,506,687', 'Page 1 of 850,669']);
  });

  it('says nothing is shown when there are no results', async () => {
    const { texts, prev, next } = await render(1, 10, 0, 0);
    expect(texts()).toEqual(['0 of 0', 'Page 1 of 0']);
    expect(prev.disabled).toBe(true);
    expect(next.disabled).toBe(true);
  });

  it('names its controls for screen readers', async () => {
    const { el, prev, next } = await render(1, 10, 25, 10);
    expect(prev.getAttribute('aria-label')).toBe('Previous page');
    expect(next.getAttribute('aria-label')).toBe('Next page');
    expect(el.querySelector('select')!.getAttribute('aria-label')).toBe('Voters per page');
  });

  it('disables Previous on the first page and moves forward with Next', async () => {
    const { fixture, component, prev, next, texts } = await render(1, 10, 25, 10);
    expect(prev.disabled).toBe(true);
    expect(next.disabled).toBe(false);

    next.click();
    fixture.detectChanges();
    expect(component.pageNumber()).toBe(2);
    expect(texts()[1]).toBe('Page 2 of 3');
    expect(prev.disabled).toBe(false);
  });

  it('disables Next on the last page and moves back with Previous', async () => {
    const { fixture, component, prev, next } = await render(3, 10, 25, 5);
    expect(next.disabled).toBe(true);
    prev.click();
    fixture.detectChanges();
    expect(component.pageNumber()).toBe(2);
  });

  it('changes the page size from the dropdown', async () => {
    const { fixture, component, el, texts } = await render(1, 10, 25, 10);
    const select = el.querySelector('select') as HTMLSelectElement;
    expect(select.value).toBe('10');

    select.value = '5';
    select.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    expect(Number(component.pageSize())).toBe(5);
    expect(texts()[1]).toBe('Page 1 of 5');
  });
});
