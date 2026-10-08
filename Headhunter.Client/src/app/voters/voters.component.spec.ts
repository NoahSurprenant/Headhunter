import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { API_UNAVAILABLE, stubFetch } from '../../testing/fetch-stub';
import { voterRow, votersPage } from '../../testing/synthetic-data';
import { VotersComponent } from './voters.component';

describe('VotersComponent', () => {
  const page = votersPage([
    voterRow({ id: 1001, firstName: 'Alex', middleName: 'Quinn', lastName: 'Example', gender: 'F' }),
    voterRow({ id: 1002, firstName: 'Jordan', lastName: 'Sample', birthYear: 1985, directionPrefix: 'N', streetName: 'Pretend', streetType: 'Ave' }),
  ], 42);

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [VotersComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
  });

  afterEach(() => vi.unstubAllGlobals());

  // Zoneless: no manual detectChanges(). whenStable() waits for the scheduled
  // change detection, the resource fetch and the resulting re-render.
  async function settle(fixture: ComponentFixture<VotersComponent>) {
    await fixture.whenStable();
  }

  async function render() {
    const fixture = TestBed.createComponent(VotersComponent);
    await settle(fixture);
    return { fixture, component: fixture.componentInstance, el: fixture.nativeElement as HTMLElement };
  }

  const lastCall = (fetchMock: ReturnType<typeof stubFetch>) => fetchMock.mock.calls.at(-1)!;

  it('posts the first page without a filter and renders each voter as a row', async () => {
    const fetchMock = stubFetch(() => page);
    const { el } = await render();

    const [url, init] = lastCall(fetchMock);
    expect(url).toBe('Api/voters?pageSize=10&pageNumber=1');
    expect(init?.method).toBe('POST');
    expect(init?.body).toBeUndefined();

    const text = el.textContent!.replace(/\s+/g, ' ');
    expect(text).toContain('Alex Quinn Example');
    expect(text).toContain('Jordan Sample');
    expect(text).toContain('N Pretend Ave');
    expect(text).toContain('Items 1 - 2 of 42');
    const links = Array.from(el.querySelectorAll('a')).map(a => a.getAttribute('href'));
    expect(links).toEqual(['/voter/1001', '/voter/1002']);
  });

  it('keeps the previous rows when a later request fails', async () => {
    let fail = false;
    const fetchMock = stubFetch(() => (fail ? API_UNAVAILABLE : page));
    const { fixture, component, el } = await render();
    fail = true;
    component.clicked();
    await settle(fixture);

    expect(fetchMock.mock.calls.length).toBeGreaterThan(1);
    expect(el.textContent).toContain('Jordan Sample');
  });

  it('submits the search form as a filter, converting the numeric fields', async () => {
    const fetchMock = stubFetch(() => page);
    const { fixture, component } = await render();

    component.formGroup.patchValue({ lastName: 'Example', city: 'Testville', astrology: 'Leo' });
    // <x-input type="number"> hands the control a string; onSubmit must convert it.
    component.formGroup.controls.birthYear.setValue('1990' as unknown as number);
    component.onSubmit();
    await settle(fixture);

    expect(JSON.parse(lastCall(fetchMock)[1]!.body as string)).toEqual({
      firstName: '',
      middleName: '',
      lastName: 'Example',
      street: '',
      city: 'Testville',
      birthYear: 1990,
      age: null,
      astrology: 'Leo',
    });
  });

  it('clears the form and the filter with Clear All', async () => {
    const fetchMock = stubFetch(() => page);
    const { fixture, component } = await render();
    component.formGroup.patchValue({ firstName: 'Alex' });
    component.onSubmit();
    await settle(fixture);

    component.clearAll();
    await settle(fixture);

    expect(component.formGroup.getRawValue().firstName).toBe('');
    expect(component.searchFilter()).toBeUndefined();
    expect(lastCall(fetchMock)[1]?.body).toBeUndefined();
  });

  it('requests the next page when the paginator moves forward', async () => {
    const fetchMock = stubFetch(() => page);
    const { fixture, el } = await render();
    const next = Array.from(el.querySelectorAll('x-paginator button')).at(-1) as HTMLButtonElement;
    next.click();
    await settle(fixture);

    expect(lastCall(fetchMock)[0]).toBe('Api/voters?pageSize=10&pageNumber=2');
  });

  it('builds the address column from the non-empty street parts', async () => {
    stubFetch(() => page);
    const { component } = await render();
    expect(component.addressColumn(voterRow({
      streetNumberPrefix: 'A', streetNumber: '12', streetNumberSuffix: '1/2', directionPrefix: 'W',
      streetName: 'Fake', streetType: 'St', directionSuffix: 'NE', extension: 'Apt 3',
    }))).toBe('A 12 1/2 W Fake St NE Apt 3');
    expect(component.addressColumn(voterRow())).toBe('123 Fake St');
  });

  it('fetches name, street and city suggestions from the API', async () => {
    stubFetch(() => page);
    const { component } = await render();
    const http = TestBed.inject(HttpTestingController);

    const lookups: [keyof VotersComponent, string][] = [
      ['firstNameSuggestions', '/api/firstNameSuggestions?query=Al'],
      ['middleNameSuggestions', '/api/middleNameSuggestions?query=Al'],
      ['lastNameSuggestions', '/api/lastNameSuggestions?query=Al'],
      ['streetSuggestions', '/api/streetSuggestions?query=Al'],
      ['citySuggestions', '/api/citySuggestions?query=Al'],
    ];
    for (const [method, url] of lookups) {
      let result: string[] | undefined;
      (component[method] as (q: string) => ReturnType<VotersComponent['citySuggestions']>)('Al')
        .subscribe(r => (result = r));
      const req = http.expectOne(url);
      expect(req.request.method).toBe('GET');
      req.flush(['Alpha', 'Alder']);
      expect(result).toEqual(['Alpha', 'Alder']);
    }
    http.verify();
  });

  it('shows live name suggestions once the HTTP response arrives', async () => {
    stubFetch(() => page);
    const { fixture, el } = await render();
    const http = TestBed.inject(HttpTestingController);
    const firstName = el.querySelector('x-input[formcontrolname="firstName"] input') as HTMLInputElement;

    vi.useFakeTimers();
    try {
      firstName.value = 'Al';
      firstName.dispatchEvent(new Event('input'));
      firstName.dispatchEvent(new Event('focus'));
      await vi.advanceTimersByTimeAsync(300); // InputComponent debounce
    } finally {
      vi.useRealTimers();
    }

    http.expectOne('/api/firstNameSuggestions?query=Al').flush(['Alex', 'Alma']);
    await fixture.whenStable();

    const items = Array.from(el.querySelectorAll('x-input[formcontrolname="firstName"] li'));
    expect(items.map(li => li.textContent!.trim())).toEqual(['Alex', 'Alma']);
    http.verify();
  });
});
