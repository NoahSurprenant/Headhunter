import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { API_UNAVAILABLE, stubFetch } from '../../testing/fetch-stub';
import { voterRow, votersPage } from '../../testing/synthetic-data';
import { VotersComponent } from './voters.component';

// The page embeds the globe; Cesium itself needs WebGL, which jsdom has not. The shared fake (one per worker).
vi.mock('cesium', async () => (await import('../../testing/fake-cesium')).fakeCesium().module);

describe('VotersComponent', () => {
  const page = votersPage([
    voterRow({ id: 'voter-1001', firstName: 'Alex', middleName: 'Quinn', lastName: 'Example', gender: 'F' }),
    voterRow({ id: 'voter-1002', firstName: 'Jordan', lastName: 'Sample', birthYear: 1985, directionPrefix: 'N', streetName: 'Pretend', streetType: 'Ave' }),
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
    const links = Array.from(el.querySelectorAll('li a')).map(a => a.getAttribute('href'));
    expect(links).toEqual(['/voter/voter-1001', '/voter/voter-1002']);
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

  it('flies the globe beside the list to a picked voter\'s address', async () => {
    stubFetch(() => votersPage([voterRow({ id: 'voter-1001', addressId: 'addr-0009', latitude: 42.3, longitude: -83.1 })]));
    const { fixture, component, el } = await render();
    const { fakeCesium } = await import('../../testing/fake-cesium');
    const viewer = fakeCesium().state.viewer!;
    viewer.camera.flyTo.mockClear();

    (el.querySelector('li') as HTMLElement).click();
    await fixture.whenStable();

    expect(component.selected()?.id).toBe('voter-1001');
    expect(el.querySelector('li')!.classList).toContain('bg-purple-100');
    expect(viewer.camera.flyTo).toHaveBeenCalledWith(expect.objectContaining({ destination: { lon: -83.1, lat: 42.3, height: 350 } }));
  });

  describe('selected area', () => {
    const box = { west: -84, east: -83, north: 43, south: 42 };
    const body = (fetchMock: ReturnType<typeof stubFetch>) => {
      const sent = lastCall(fetchMock)[1]?.body;
      return sent === undefined ? undefined : JSON.parse(sent as string);
    };

    it('lists only the voters inside a box drawn on the globe, from the first page, and keeps it in the URL', async () => {
      const fetchMock = stubFetch(() => page);
      const { fixture, component, el } = await render();
      const router = TestBed.inject(Router);
      component.pageNumber.set(3);

      // Through the globe: Select area, then a drag (the fake's screen positions are { x: longitude, y: latitude }).
      const { fakeCesium } = await import('../../testing/fake-cesium');
      const handlers = fakeCesium().state.handlers;
      (el.querySelector('app-cesium button.area-toggle') as HTMLButtonElement).click();
      handlers['LEFT_DOWN']({ position: { x: -84, y: 43 } });
      handlers['LEFT_UP']({ position: { x: -83, y: 42 } });
      await settle(fixture);

      expect(component.area()).toEqual(box);
      expect(lastCall(fetchMock)[0]).toBe('Api/voters?pageSize=10&pageNumber=1');
      expect(body(fetchMock)).toEqual(box);
      expect(router.url).toBe('/?west=-84&east=-83&north=43&south=42');
      expect(el.querySelector('.area-chip')!.textContent).toContain('In selected area');
    });

    it('combines the box with the search form', async () => {
      const fetchMock = stubFetch(() => page);
      const { fixture, component } = await render();
      component.formGroup.patchValue({ lastName: 'Example' });
      component.onSubmit();
      component.selectArea(box);
      await settle(fixture);

      expect(body(fetchMock)).toEqual(expect.objectContaining({ lastName: 'Example', firstName: '', ...box }));
    });

    it('removes the box with the chip\'s ×', async () => {
      const fetchMock = stubFetch(() => page);
      const { fixture, component, el } = await render();
      component.selectArea(box);
      await settle(fixture);

      (el.querySelector('.area-chip button') as HTMLButtonElement).click();
      await settle(fixture);

      expect(component.area()).toBeUndefined();
      expect(body(fetchMock)).toBeUndefined();
      expect(el.querySelector('.area-chip')).toBeNull();
      expect(TestBed.inject(Router).url).toBe('/');
    });

    it('is cleared by Clear All too', async () => {
      stubFetch(() => page);
      const { fixture, component } = await render();
      component.selectArea(box);
      await settle(fixture);

      component.clearAll();
      await settle(fixture);
      expect(component.area()).toBeUndefined();
    });

    it('links Export CSV to the whole current result', async () => {
      stubFetch(() => page);
      const { fixture, component, el } = await render();
      const link = () => el.querySelector('a[download]') as HTMLAnchorElement;
      expect(link().getAttribute('href')).toBe('Api/VotersCsv');
      expect(link().textContent).toContain('Export CSV');

      component.formGroup.patchValue({ lastName: 'Example', astrology: 'Leo' });
      component.onSubmit();
      component.selectArea({ west: 179, east: -179, north: 11, south: 9 });
      await settle(fixture);

      // Empty fields are left out; the API treats them as no filter.
      expect(link().getAttribute('href'))
        .toBe('Api/VotersCsv?lastName=Example&astrology=Leo&west=179&east=-179&north=11&south=9');
    });
  });
});

describe('VotersComponent opened with an area in the URL', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [VotersComponent],
      providers: [provideRouter([{ path: '', component: VotersComponent }]), provideHttpClient(), provideHttpClientTesting()],
    });
  });

  afterEach(() => vi.unstubAllGlobals());

  it('lists the voters inside it straight away and outlines it on the globe', async () => {
    const fetchMock = stubFetch(() => votersPage([voterRow()]));
    const harness = await RouterTestingHarness.create('/?west=-84&east=-83&north=43&south=42');
    await harness.fixture.whenStable();

    expect(JSON.parse(fetchMock.mock.calls.at(-1)![1]!.body as string)).toEqual({ west: -84, east: -83, north: 43, south: 42 });
    const { fakeCesium } = await import('../../testing/fake-cesium');
    expect(fakeCesium().state.viewer!.entities.getById('selected-area')).toBeDefined();
  });
});
