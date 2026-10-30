import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { API_UNAVAILABLE, stubFetch } from '../../testing/fetch-stub';
import { relative, voterDetail } from '../../testing/synthetic-data';
import { VoterComponent } from './voter.component';

describe('VoterComponent', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [VoterComponent],
      providers: [provideRouter([])],
    });
  });

  afterEach(() => vi.unstubAllGlobals());

  async function render(id: string) {
    const fixture = TestBed.createComponent(VoterComponent);
    fixture.componentRef.setInput('id', id);
    await fixture.whenStable(); // zoneless: no manual detectChanges()
    return fixture.nativeElement as HTMLElement;
  }

  // Distinguishes the three GETs the page makes: the voter, their household, and their possible relatives.
  function respond(household: unknown, relatives: unknown = []) {
    return (url: string) => {
      if (url.endsWith('/relatives')) return relatives;
      if (url.startsWith('api/address/')) return household;
      return voterDetail({ id: 'voter-0001' });
    };
  }

  it('loads the voter by id and shows them with their address, map and household', async () => {
    const household = [
      voterDetail({ id: 'voter-0001' }),
      voterDetail({ id: 'voter-0002', firstName: 'Jordan', middleName: '', lastName: 'Sample', birthYear: 1985, gender: 'M' }),
    ];
    const fetchMock = stubFetch(url => respond(household)(String(url)));
    const el = await render('voter-0001');
    const text = el.textContent!.replace(/\s+/g, ' ');

    expect(fetchMock.mock.calls[0][0]).toBe('api/voter/voter-0001');
    expect(fetchMock.mock.calls[0][1]?.method).toBe('GET');
    expect(fetchMock.mock.calls.map(c => c[0])).toContain('api/address/addr-0001');
    expect(fetchMock.mock.calls.map(c => c[0])).toContain('api/voter/voter-0001/relatives');
    expect(el.querySelector('h1')!.textContent).toContain('Alex Quinn Example');
    expect(text).toContain('(born 1990)');
    expect(text).toContain('Female');
    expect(el.querySelector('a[href="/address/addr-0001"]')).not.toBeNull();
    expect(el.querySelector('a[href^="/globe"]')!.getAttribute('href')).toBe('/globe?lat=43&lon=-84.5&address=addr-0001');
    expect(el.querySelector('app-map-preview')).not.toBeNull();
    // The household, without the voter themselves.
    expect([...el.querySelectorAll('ul a[href^="/voter/"]')].map(a => a.getAttribute('href'))).toEqual(['/voter/voter-0002']);
    expect(text).not.toContain('No Voter Loaded');
    expect(text).toContain('No matches nearby');
  });

  it('shows possible relatives with distance and address, linking to their voter page and the globe when located', async () => {
    const relatives = [
      relative({
        id: 'voter-0003', firstName: 'Robin', lastName: 'Example', distanceKm: 1.3,
        addressId: 'addr-0002', latitude: 43.01, longitude: -84.51, fullStreetAddress: '456 Nearby Rd', city: 'Testville',
      }),
      relative({
        id: 'voter-0004', firstName: 'Casey', lastName: 'Example', distanceKm: null,
        addressId: 'addr-0003', latitude: null, longitude: null, fullStreetAddress: '1 Faraway Rd', city: 'Farplace',
      }),
    ];
    stubFetch(url => respond([], relatives)(String(url)));
    const el = await render('voter-0001');
    const text = el.textContent!.replace(/\s+/g, ' ');

    expect(text).toContain('Possible relatives');
    expect(el.querySelector('a[href="/voter/voter-0003"]')).not.toBeNull();
    expect(el.querySelector('a[href="/voter/voter-0004"]')).not.toBeNull();
    expect(text).toContain('1.3 km away');
    expect(text).toContain('456 Nearby Rd, Testville');
    // Only the located relative (with coordinates) gets a globe link.
    const globeHrefs = [...el.querySelectorAll('a[href^="/globe"]')].map(a => a.getAttribute('href'));
    expect(globeHrefs).toContain('/globe?lat=43.01&lon=-84.51&address=addr-0002');
    expect(globeHrefs.some(h => h?.includes('addr-0003'))).toBe(false);
  });

  it('shows the empty state when the request fails', async () => {
    stubFetch(() => API_UNAVAILABLE);
    const el = await render('42');

    expect(el.querySelector('h1')!.textContent).toBe('Voter');
    expect(el.textContent).toContain('No Voter Loaded');
  });

  it('shows how long ago the voter registered alongside the date', async () => {
    const twoYearsAgo = new Date();
    twoYearsAgo.setFullYear(twoYearsAgo.getFullYear() - 2);
    stubFetch(url =>
      String(url).startsWith('api/address/')
        ? [voterDetail({ id: 'voter-0001' })]
        : voterDetail({ id: 'voter-0001', registrationDate: twoYearsAgo.toISOString() }),
    );
    const el = await render('voter-0001');

    expect(el.textContent).toContain('2 years ago');
  });

  it('shows "Registration date unknown" instead of a 1900-01-01 placeholder', async () => {
    stubFetch(url =>
      String(url).startsWith('api/address/')
        ? [voterDetail({ id: 'voter-0001' })]
        : voterDetail({ id: 'voter-0001', registrationDate: '1900-01-01T00:00:00' }),
    );
    const el = await render('voter-0001');

    expect(el.textContent).toContain('Registration date unknown');
  });
});
