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

  it('loads the voter by id and renders the voter card', async () => {
    const fetchMock = stubFetch(() => voterDetail());
    const el = await render('42');

    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(fetchMock.mock.calls[0][0]).toBe('api/voter/42');
    expect(fetchMock.mock.calls[0][1]?.method).toBe('GET');
    expect(el.querySelector('app-voter-card')).not.toBeNull();
    expect(el.textContent).toContain('Alex');
    expect(el.textContent).not.toContain('No Voter Loaded');
  });

  it('shows the empty state when the request fails', async () => {
    stubFetch(() => API_UNAVAILABLE);
    const el = await render('42');

    expect(el.querySelector('app-voter-card')).toBeNull();
    expect(el.textContent).toContain('No Voter Loaded');
  });
});
