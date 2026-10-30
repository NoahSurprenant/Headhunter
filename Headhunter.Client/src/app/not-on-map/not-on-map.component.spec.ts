import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { stubFetch } from '../../testing/fetch-stub';
import { NotOnMapComponent } from './not-on-map.component';

describe('NotOnMapComponent', () => {
  const counties = [
    { county: 'Ingham', count: 2 },
    { county: 'Genesee', count: 1 },
  ];
  const page = {
    totalCount: 3,
    results: [
      { id: 'addr-0005', fullStreetAddress: '300 Pine Ln', city: 'Lansing', state: 'MI', zipCode: '00000', county: 'Ingham', matched: false },
      { id: 'addr-0006', fullStreetAddress: '301 Birch Ln', city: 'Lansing', state: 'MI', zipCode: '00000', county: 'Ingham', matched: null },
    ],
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [NotOnMapComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
  });

  afterEach(() => vi.unstubAllGlobals());

  async function render() {
    const fixture = TestBed.createComponent(NotOnMapComponent);
    await fixture.whenStable();
    return { fixture, component: fixture.componentInstance, el: fixture.nativeElement as HTMLElement };
  }

  it('shows the per-county counts and the first page of addresses, linking to their address page', async () => {
    stubFetch(url => (String(url).includes('counties') ? counties : page));
    const { el } = await render();

    const text = el.textContent!.replace(/\s+/g, ' ');
    expect(text).toContain("3 addresses the geocoder hasn't placed.");
    expect(text).toContain('Ingham (2)');
    expect(text).toContain('Genesee (1)');
    expect(text).toContain('300 Pine Ln');
    expect(text).toContain('Failed to match');
    expect(text).toContain('Not yet attempted');
    expect(el.querySelector('a[href="/address/addr-0005"]')).not.toBeNull();
  });

  it('filters by county when a county chip is clicked, and clears it on a second click', async () => {
    const fetchMock = stubFetch(url => (String(url).includes('counties') ? counties : page));
    const { fixture, el } = await render();
    const inghamChip = [...el.querySelectorAll('button')].find(b => b.textContent?.includes('Ingham'))!;

    inghamChip.click();
    await fixture.whenStable();
    expect(fetchMock.mock.calls.at(-1)![0]).toContain('county=Ingham');

    inghamChip.click();
    await fixture.whenStable();
    expect(fetchMock.mock.calls.at(-1)![0]).not.toContain('county=');
  });

  it('filters by city from the input, debounced, and resets to page 1', async () => {
    const fetchMock = stubFetch(url => (String(url).includes('counties') ? counties : page));
    const { fixture, component } = await render();
    component.pageNumber.set(2);
    await fixture.whenStable();

    component.cityControl.setValue('Lansing');
    // Real delay past the 300ms debounce: mixing fake timers with the resource()'s async reload
    // deadlocked fixture.whenStable() (the scheduled reload never ran once real timers came back).
    await new Promise(resolve => setTimeout(resolve, 350));
    await fixture.whenStable();

    const [url] = fetchMock.mock.calls.at(-1)!;
    expect(url).toContain('city=Lansing');
    expect(url).toContain('pageNumber=1');
  });

  it('shows Clear filters only once a filter is set, and clears it', async () => {
    stubFetch(url => (String(url).includes('counties') ? counties : page));
    const { fixture, component, el } = await render();
    expect([...el.querySelectorAll('button')].some(b => b.textContent?.includes('Clear filters'))).toBe(false);

    component.selectCounty('Ingham');
    await fixture.whenStable();

    const clear = [...el.querySelectorAll('button')].find(b => b.textContent?.includes('Clear filters'));
    expect(clear).not.toBeUndefined();
    clear!.click();
    await fixture.whenStable();

    expect(component.countyFilter()).toBe('');
    expect([...el.querySelectorAll('button')].some(b => b.textContent?.includes('Clear filters'))).toBe(false);
  });

  it('requests the next page when the paginator moves forward', async () => {
    // More rows than fit on one page (pageSize 10), so the paginator's next button is enabled.
    const manyRows = { ...page, totalCount: 25 };
    const fetchMock = stubFetch(url => (String(url).includes('counties') ? counties : manyRows));
    const { fixture, el } = await render();
    const next = [...el.querySelectorAll('x-paginator button')].at(-1) as HTMLButtonElement;

    next.click();
    await fixture.whenStable();

    expect(fetchMock.mock.calls.at(-1)![0]).toContain('pageNumber=2');
  });

  it('shows an empty state when there is nothing missing', async () => {
    stubFetch(url => (String(url).includes('counties') ? [] : { totalCount: 0, results: [] }));
    const { el } = await render();

    expect(el.textContent).toContain('Nothing missing.');
    expect(el.textContent).toContain('No addresses match these filters.');
  });
});
