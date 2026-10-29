import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { API_UNAVAILABLE, stubFetch } from '../../testing/fetch-stub';
import { voterDetail } from '../../testing/synthetic-data';
import { AddressComponent } from './address.component';

describe('AddressComponent', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [AddressComponent],
      providers: [provideRouter([])],
    });
  });

  afterEach(() => vi.unstubAllGlobals());

  async function render(id: string) {
    const fixture = TestBed.createComponent(AddressComponent);
    fixture.componentRef.setInput('id', id);
    await fixture.whenStable(); // zoneless: no manual detectChanges()
    return fixture.nativeElement as HTMLElement;
  }

  it('loads everyone registered at the address and lists each, linking to their page', async () => {
    const fetchMock = stubFetch(() => [
      voterDetail(),
      voterDetail({ id: 'voter-0002', firstName: 'Jordan', middleName: '', lastName: 'Sample', birthYear: 1985 }),
    ]);
    const el = await render('addr-0001');
    const text = el.textContent!.replace(/\s+/g, ' ');

    expect(fetchMock.mock.calls[0][0]).toBe('api/address/addr-0001');
    expect(text).toContain('2 voters registered here');
    expect(text).toContain('123 Fake St');
    expect(text).toContain('Testville, MI 49999');
    expect(text).toContain('Alex Quinn Example');
    expect([...el.querySelectorAll('a[href^="/voter/"]')].map(a => a.getAttribute('href')))
      .toEqual(['/voter/voter-0001', '/voter/voter-0002']);
    // Show on globe: the address's position and id.
    expect(el.querySelector('a[href^="/globe"]')!.getAttribute('href')).toBe('/globe?lat=43&lon=-84.5&address=addr-0001');
    expect(el.querySelector('app-map-preview')).not.toBeNull();
  });

  it('renders an empty page instead of throwing when the request fails', async () => {
    stubFetch(() => API_UNAVAILABLE);
    const el = await render('addr-0001');

    expect(el.textContent).toContain('No voters registered at this address.');
    expect(el.querySelectorAll('a[href^="/voter/"]').length).toBe(0);
  });
});
