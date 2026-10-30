import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { API_UNAVAILABLE, stubFetch } from '../../testing/fetch-stub';
import { voterDetail } from '../../testing/synthetic-data';
import { AddressComponent, WalkDto } from './address.component';

/** Answers the address and walk requests separately so each test can shape both independently. */
function stubAddressAndWalk(voters: unknown[], walk: WalkDto) {
  return stubFetch(url => (String(url).includes('/walk') ? walk : voters));
}

const noWalk: WalkDto = { previous: null, next: null, nearby: [] };

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

  it('walks the street: previous/next houses on the same side by default, linking to their page', async () => {
    const fetchMock = stubAddressAndWalk([voterDetail()], {
      previous: { id: 'addr-0000', fullStreetAddress: '121 Fake St' },
      next: { id: 'addr-0002', fullStreetAddress: '125 Fake St' },
      nearby: [],
    });
    const el = await render('addr-0001');

    expect(fetchMock.mock.calls.map(c => String(c[0])))
      .toContain('api/address/addr-0001/walk?sameSideOnly=true');
    expect(el.querySelector('a[href="/address/addr-0000"]')!.textContent).toContain('121 Fake St');
    expect(el.querySelector('a[href="/address/addr-0002"]')!.textContent).toContain('125 Fake St');
  });

  it('shows no previous/next house at the ends of the block instead of a broken link', async () => {
    stubAddressAndWalk([voterDetail()], noWalk);
    const el = await render('addr-0001');
    const text = el.textContent!.replace(/\s+/g, ' ');

    expect(text).toContain('No previous house');
    expect(text).toContain('No next house');
  });

  it('toggling "both sides of the street" refetches without the same-side filter', async () => {
    const fetchMock = stubAddressAndWalk([voterDetail()], noWalk);
    const fixture = TestBed.createComponent(AddressComponent);
    fixture.componentRef.setInput('id', 'addr-0001');
    await fixture.whenStable();
    const checkbox = (fixture.nativeElement as HTMLElement).querySelector('input[type=checkbox]') as HTMLInputElement;

    checkbox.click();
    await fixture.whenStable();

    expect(fetchMock.mock.calls.map(c => String(c[0])))
      .toContain('api/address/addr-0001/walk?sameSideOnly=false');
  });

  it('lists nearby households ordered by distance, with voter counts, linking to their page', async () => {
    stubAddressAndWalk([voterDetail()], {
      previous: null,
      next: null,
      nearby: [
        { id: 'addr-0005', fullStreetAddress: '200 Nearby Ave', voterCount: 2, distanceMeters: 30 },
        { id: 'addr-0006', fullStreetAddress: '202 Nearby Ave', voterCount: 1, distanceMeters: 2000 },
      ],
    });
    const el = await render('addr-0001');
    const text = el.textContent!.replace(/\s+/g, ' ');

    expect(text).toContain('200 Nearby Ave');
    expect(text).toContain('98 ft');
    expect(text).toContain('2 voters');
    expect(text).toContain('202 Nearby Ave');
    expect(text).toContain('1.2 mi');
    expect(text).toContain('1 voter');
    expect(el.querySelector('a[href="/address/addr-0005"]')).not.toBeNull();
  });

  it('walks to the previous/next house with the ArrowLeft/ArrowRight keys', async () => {
    stubAddressAndWalk([voterDetail()], {
      previous: { id: 'addr-0000', fullStreetAddress: '121 Fake St' },
      next: { id: 'addr-0002', fullStreetAddress: '125 Fake St' },
      nearby: [],
    });
    await render('addr-0001');
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowRight' }));
    expect(navigate).toHaveBeenCalledWith(['/address', 'addr-0002']);

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowLeft' }));
    expect(navigate).toHaveBeenCalledWith(['/address', 'addr-0000']);
  });
});
