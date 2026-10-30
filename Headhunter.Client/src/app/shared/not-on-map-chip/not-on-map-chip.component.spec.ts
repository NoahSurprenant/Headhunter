import { TestBed } from '@angular/core/testing';
import { API_UNAVAILABLE, stubFetch } from '../../../testing/fetch-stub';
import { NotOnMapChipComponent } from './not-on-map-chip.component';

describe('NotOnMapChipComponent', () => {
  afterEach(() => vi.unstubAllGlobals());

  function render(addressId = 'addr-0001') {
    const fixture = TestBed.createComponent(NotOnMapChipComponent);
    fixture.componentRef.setInput('addressId', addressId);
    fixture.detectChanges();
    return fixture;
  }

  it('shows the "Not on the map" chip with a retry button', () => {
    const fixture = render();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Not on the map');
    expect(el.querySelector('button')).not.toBeNull();
  });

  it('retries geocoding and emits the new coordinates on a match', async () => {
    const fetchMock = stubFetch(() => ({ matched: true, latitude: 42.5, longitude: -83.5 }));
    const fixture = render('addr-0042');
    let emitted: { latitude: number; longitude: number } | undefined;
    fixture.componentInstance.geocoded.subscribe(e => (emitted = e));

    (fixture.nativeElement.querySelector('button') as HTMLButtonElement).click();
    await fixture.whenStable();

    expect(fetchMock.mock.calls[0][0]).toBe('api/address/addr-0042/retrygeocode');
    expect(fetchMock.mock.calls[0][1]?.method).toBe('POST');
    expect(emitted).toEqual({ latitude: 42.5, longitude: -83.5 });
    expect(fixture.nativeElement.textContent).toContain('Found it!');
  });

  it('shows a message when the retry still finds no match', async () => {
    stubFetch(() => ({ matched: false, latitude: null, longitude: null }));
    const fixture = render();

    (fixture.nativeElement.querySelector('button') as HTMLButtonElement).click();
    await fixture.whenStable();

    expect(fixture.nativeElement.textContent).toContain('Still not found');
  });

  it('shows a message when the request fails', async () => {
    stubFetch(() => API_UNAVAILABLE);
    const fixture = render();

    (fixture.nativeElement.querySelector('button') as HTMLButtonElement).click();
    await fixture.whenStable();

    expect(fixture.nativeElement.textContent).toContain('Retry failed');
  });

  it('shows a friendly message when the server rate limits the retry (429)', async () => {
    const fetchMock = vi.fn(async () => new Response(null, { status: 429 }));
    vi.stubGlobal('fetch', fetchMock);
    const fixture = render();

    (fixture.nativeElement.querySelector('button') as HTMLButtonElement).click();
    await fixture.whenStable();

    expect(fixture.nativeElement.textContent).toContain('Too many retries, try again in a minute.');
  });
});
