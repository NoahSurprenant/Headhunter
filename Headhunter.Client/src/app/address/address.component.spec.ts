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

  it('loads everyone registered at the address and renders a card for each', async () => {
    const fetchMock = stubFetch(() => [
      voterDetail(),
      voterDetail({ firstName: 'Jordan', middleName: '', lastName: 'Sample', birthYear: 1985 }),
    ]);
    const el = await render('addr-0001');
    const text = el.textContent!.replace(/\s+/g, ' ');

    expect(fetchMock.mock.calls[0][0]).toBe('api/address/addr-0001');
    expect(text).toContain('2 voters at this address');
    expect(text).toContain('123 Fake St');
    expect(text).toContain('Testville, MI 49999');
    expect(el.querySelectorAll('app-voter-card').length).toBe(2);
    // The cards are on the address page already, so they must not link back to it.
    expect(el.querySelectorAll('app-voter-card a').length).toBe(0);
  });

  it('renders an empty page instead of throwing when the request fails', async () => {
    stubFetch(() => API_UNAVAILABLE);
    const el = await render('addr-0001');

    expect(el.textContent).toContain('0 voters at this address');
    expect(el.querySelectorAll('app-voter-card').length).toBe(0);
  });
});
