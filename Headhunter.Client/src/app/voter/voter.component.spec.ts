import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { API_UNAVAILABLE, stubFetch } from '../../testing/fetch-stub';
import { voterDetail } from '../../testing/synthetic-data';
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

  it('loads the voter by id and shows them with their address, map and household', async () => {
    const household = [
      voterDetail({ id: 'voter-0001' }),
      voterDetail({ id: 'voter-0002', firstName: 'Jordan', middleName: '', lastName: 'Sample', birthYear: 1985, gender: 'M' }),
    ];
    const fetchMock = stubFetch(url => (String(url).startsWith('api/address/') ? household : voterDetail({ id: 'voter-0001' })));
    const el = await render('voter-0001');
    const text = el.textContent!.replace(/\s+/g, ' ');

    expect(fetchMock.mock.calls[0][0]).toBe('api/voter/voter-0001');
    expect(fetchMock.mock.calls[0][1]?.method).toBe('GET');
    expect(fetchMock.mock.calls.map(c => c[0])).toContain('api/address/addr-0001');
    expect(el.querySelector('h1')!.textContent).toContain('Alex Quinn Example');
    expect(text).toContain('(born 1990)');
    expect(text).toContain('Female');
    expect(el.querySelector('a[href="/address/addr-0001"]')).not.toBeNull();
    expect(el.querySelector('a[href^="/globe"]')!.getAttribute('href')).toBe('/globe?lat=43&lon=-84.5&address=addr-0001');
    expect(el.querySelector('app-map-preview')).not.toBeNull();
    // The household, without the voter themselves.
    expect([...el.querySelectorAll('ul a[href^="/voter/"]')].map(a => a.getAttribute('href'))).toEqual(['/voter/voter-0002']);
    expect(text).not.toContain('No Voter Loaded');
  });

  it('shows the empty state when the request fails', async () => {
    stubFetch(() => API_UNAVAILABLE);
    const el = await render('42');

    expect(el.querySelector('h1')!.textContent).toBe('Voter');
    expect(el.textContent).toContain('No Voter Loaded');
  });
});
