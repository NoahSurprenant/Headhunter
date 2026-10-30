import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { API_UNAVAILABLE, stubFetch } from '../../testing/fetch-stub';
import { districtStats, voterRow, votersPage } from '../../testing/synthetic-data';
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
    expect(text).toContain('1–2 of 42');
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
      districtType: null,
      district: '',
      districtWithin: '',
    });
  });

  it('clears the form and the filter with Clear All', async () => {
    const fetchMock = stubFetch(() => page);
    const { fixture, component } = await render();
    component.formGroup.patchValue({ firstName: 'Alex' });
    component.mailingAddressDiffers.set(true);
    component.onSubmit();
    await settle(fixture);

    component.clearAll();
    await settle(fixture);

    expect(component.formGroup.getRawValue().firstName).toBe('');
    expect(component.searchFilter()).toBeUndefined();
    expect(component.mailingAddressDiffers()).toBe(false);
    expect(lastCall(fetchMock)[1]?.body).toBeUndefined();
    expect(lastCall(fetchMock)[0]).toBe('Api/voters?pageSize=10&pageNumber=1');
  });

  it('flags rows that have a mailing address, and out-of-state ones', async () => {
    const flaggedPage = votersPage([
      voterRow({ id: 'voter-2001', firstName: 'Dana', lastName: 'Elsewhere', hasMailingAddress: true, mailingAddressOutOfState: true }),
      voterRow({ id: 'voter-2002', firstName: 'Jamie', lastName: 'InState', hasMailingAddress: true, mailingAddressOutOfState: false }),
      voterRow({ id: 'voter-2003', firstName: 'Robin', lastName: 'AtHome', hasMailingAddress: false, mailingAddressOutOfState: false }),
    ]);
    stubFetch(() => flaggedPage);
    const { el } = await render();

    const rows = Array.from(el.querySelectorAll('li'));
    const chipsFor = (name: string) =>
      rows.find(li => li.textContent!.includes(name))!.querySelectorAll('.chip');

    expect(chipsFor('Dana')).toHaveLength(2);
    expect(chipsFor('Jamie')).toHaveLength(1);
    expect(chipsFor('Robin')).toHaveLength(0);
    expect(el.textContent).toContain('Lives elsewhere');
    expect(el.textContent).toContain('Out of state');
  });

  it('sends "mailingAddressDiffers" as a query param, combinable with the rest of the search', async () => {
    const fetchMock = stubFetch(() => page);
    const { fixture, component, el } = await render();

    const checkbox = el.querySelector('input[type="checkbox"]') as HTMLInputElement;
    checkbox.checked = true;
    checkbox.dispatchEvent(new Event('change'));
    await settle(fixture);

    expect(component.mailingAddressDiffers()).toBe(true);
    expect(lastCall(fetchMock)[0]).toBe('Api/voters?pageSize=10&pageNumber=1&mailingAddressDiffers=true');

    // Still applies once a name filter is submitted too: it's its own query param, not a replacement.
    component.formGroup.patchValue({ lastName: 'Example' });
    component.onSubmit();
    await settle(fixture);

    const [url, init] = lastCall(fetchMock);
    expect(url).toBe('Api/voters?pageSize=10&pageNumber=1&mailingAddressDiffers=true');
    expect(JSON.parse(init!.body as string).lastName).toBe('Example');
  });

  it('exports only the voters whose mail goes elsewhere when that box is ticked', async () => {
    stubFetch(() => page);
    const { fixture, component, el } = await render();

    component.mailingAddressDiffers.set(true);
    await settle(fixture);

    expect((el.querySelector('a[download]') as HTMLAnchorElement).getAttribute('href')).toBe('Api/VotersCsv?mailingAddressDiffers=true');
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

  describe('district filter', () => {
    // The voters list, and the district card's stats.
    const respond = (url: string) => (url.startsWith('api/district?') ? districtStats() : page);
    const body = (fetchMock: ReturnType<typeof stubFetch>) =>
      JSON.parse(fetchMock.mock.calls.filter(c => String(c[0]).startsWith('Api/voters')).at(-1)![1]!.body as string);

    it('searches a precinct within its jurisdiction, alongside the other filters, and keeps it in the URL', async () => {
      const fetchMock = stubFetch(respond);
      const { fixture, component, el } = await render();

      component.formGroup.patchValue({ lastName: 'Example', districtType: 'Precinct', district: '00012', districtWithin: 'CITY OF TESTVILLE' });
      component.onSubmit();
      await settle(fixture);

      expect(body(fetchMock)).toEqual(expect.objectContaining({
        lastName: 'Example', districtType: 'Precinct', district: '00012', districtWithin: 'CITY OF TESTVILLE',
      }));
      expect(TestBed.inject(Router).url).toBe('/?lastName=Example&districtType=Precinct&district=00012&districtWithin=CITY%20OF%20TESTVILLE');
      expect(component.activeDistrict()).toEqual({ type: 'Precinct', name: '00012', within: 'CITY OF TESTVILLE' });
      expect(el.querySelector('app-district-card')!.textContent).toContain('1,240');
    });

    it('drops the jurisdiction for a district that needs none', async () => {
      const fetchMock = stubFetch(respond);
      const { fixture, component } = await render();

      component.formGroup.patchValue({ districtType: 'County', district: 'TESTSHIRE', districtWithin: 'leftover' });
      component.onSubmit();
      await settle(fixture);

      expect(body(fetchMock)).toEqual(expect.objectContaining({ districtType: 'County', district: 'TESTSHIRE', districtWithin: '' }));
      expect(TestBed.inject(Router).url).toBe('/?districtType=County&district=TESTSHIRE');
    });

    it('shows no card while the district is incomplete, and clears the URL with Clear All', async () => {
      stubFetch(respond);
      const { fixture, component, el } = await render();

      component.formGroup.patchValue({ districtType: 'Ward', district: '01' });
      component.onSubmit();
      await settle(fixture);
      expect(component.activeDistrict()).toBeUndefined();
      expect(el.querySelector('app-district-card')).toBeNull();

      component.clearAll();
      await settle(fixture);
      expect(TestBed.inject(Router).url).toBe('/');
    });

    it('restores the search from the URL', async () => {
      const fetchMock = stubFetch(respond);
      TestBed.inject(Router).resetConfig([{ path: 'voters', component: VotersComponent }]);
      const harness = await RouterTestingHarness.create('/voters?firstName=Alex&birthYear=1990&districtType=StateHouse&district=1ST%20DISTRICT');
      await harness.fixture.whenStable();
      const component = harness.routeDebugElement!.componentInstance as VotersComponent;

      expect(component.formGroup.getRawValue()).toEqual(expect.objectContaining({ firstName: 'Alex', districtType: 'StateHouse', district: '1ST DISTRICT' }));
      expect(body(fetchMock)).toEqual(expect.objectContaining({ firstName: 'Alex', birthYear: 1990, districtType: 'StateHouse', district: '1ST DISTRICT' }));
      expect(harness.routeNativeElement!.querySelector('app-district-card')).not.toBeNull();
    });

    it('offers a jurisdiction (or county) field only for the districts numbered within one', async () => {
      stubFetch(() => page);
      const { fixture, component, el } = await render();
      const select = el.querySelector('select#districtType') as HTMLSelectElement;
      expect(Array.from(select.options).map(o => o.textContent!.trim())).toContain('School district');

      expect(el.querySelector('x-input[formcontrolname="district"]')).toBeNull();
      select.value = 'Ward';
      select.dispatchEvent(new Event('change'));
      await settle(fixture);
      expect(component.districtWithin()).toBe('Jurisdiction');
      expect(el.querySelector('x-input[formcontrolname="districtWithin"]')).not.toBeNull();
      expect(el.querySelector('x-input[formcontrolname="district"]')).not.toBeNull();

      component.formGroup.patchValue({ district: '01', districtWithin: 'CITY OF TESTVILLE' });
      select.value = 'StateSenate';
      select.dispatchEvent(new Event('change'));
      await settle(fixture);
      // A new type starts the district over.
      expect(component.formGroup.getRawValue()).toEqual(expect.objectContaining({ districtType: 'StateSenate', district: '', districtWithin: '' }));
      expect(el.querySelector('x-input[formcontrolname="districtWithin"]')).toBeNull();
    });

    it('suggests district names of the picked type, in the picked jurisdiction or county', async () => {
      stubFetch(() => page);
      const { component } = await render();
      const http = TestBed.inject(HttpTestingController);

      component.formGroup.patchValue({ districtType: 'CountyCommissioner', districtWithin: 'TESTSHIRE' });
      component.districtSuggestions('DIS').subscribe();
      let req = http.expectOne(r => r.url === '/api/districtSuggestions');
      expect(req.request.params.get('type')).toBe('CountyCommissioner');
      expect(req.request.params.get('query')).toBe('DIS');
      expect(req.request.params.get('within')).toBe('TESTSHIRE');
      req.flush(['DISTRICT 01']);

      component.districtWithinSuggestions('TEST').subscribe();
      req = http.expectOne(r => r.url === '/api/districtSuggestions');
      expect(req.request.params.get('type')).toBe('County');
      expect(req.request.params.get('query')).toBe('TEST');
      req.flush(['TESTSHIRE']);

      component.formGroup.patchValue({ districtType: 'Precinct' });
      component.districtWithinSuggestions('CITY').subscribe();
      http.expectOne(r => r.url === '/api/districtSuggestions' && r.params.get('type') === 'Jurisdiction').flush([]);
      http.verify();
    });

    it('shows the district on the globe beside the list', async () => {
      stubFetch(respond);
      const { fixture, component } = await render();
      const http = TestBed.inject(HttpTestingController);

      component.formGroup.patchValue({ districtType: 'County', district: 'TESTSHIRE' });
      component.onSubmit();
      await settle(fixture);

      const req = http.expectOne(r => r.url === 'api/districtExtent');
      expect(req.request.params.get('name')).toBe('TESTSHIRE');
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

describe('VotersComponent advanced search', () => {
  const page = votersPage([voterRow({ id: 'voter-3001', firstName: 'Alex', lastName: 'Example' })], 1);

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [VotersComponent],
      providers: [provideRouter([{ path: '', component: VotersComponent }]), provideHttpClient(), provideHttpClientTesting()],
    });
  });

  afterEach(() => vi.unstubAllGlobals());

  async function open(url = '/') {
    const harness = await RouterTestingHarness.create(url);
    await harness.fixture.whenStable();
    const component = harness.routeDebugElement!.componentInstance as VotersComponent;
    const el = harness.routeNativeElement as HTMLElement;
    const settle = () => harness.fixture.whenStable();
    return { harness, component, el, settle };
  }

  const rows = (el: HTMLElement) => Array.from(el.querySelectorAll('.cond-row')) as HTMLElement[];
  const options = (select: Element) => Array.from((select as HTMLSelectElement).options).map(o => o.textContent!.trim());
  const choose = (select: Element, value: string) => {
    (select as HTMLSelectElement).value = value;
    select.dispatchEvent(new Event('change'));
  };
  const type = (input: Element, value: string) => {
    (input as HTMLInputElement).value = value;
    input.dispatchEvent(new Event('input'));
  };
  const votersBody = (fetchMock: ReturnType<typeof stubFetch>) => {
    const sent = fetchMock.mock.calls.filter(c => String(c[0]).startsWith('Api/voters')).at(-1)![1]?.body;
    return sent === undefined ? undefined : JSON.parse(sent as string);
  };

  it('opens with one row, adds and removes rows, and stops at ten', async () => {
    stubFetch(() => page);
    const { component, el, settle } = await open();
    expect(el.querySelector('#advanced-conditions')).toBeNull();

    (el.querySelector('.advanced-toggle') as HTMLButtonElement).click();
    await settle();
    expect(el.querySelector('.advanced-toggle')!.getAttribute('aria-expanded')).toBe('true');
    expect(rows(el)).toHaveLength(1);
    // A new row is a fast one: last name starts with
    expect((rows(el)[0].querySelector('.cond-field') as HTMLSelectElement).value).toBe('lastName');
    expect((rows(el)[0].querySelector('.cond-op') as HTMLSelectElement).value).toBe('startsWith');

    (el.querySelector('.add-condition') as HTMLButtonElement).click();
    (el.querySelector('.add-condition') as HTMLButtonElement).click();
    await settle();
    expect(rows(el)).toHaveLength(3);
    expect(el.querySelector('.advanced-toggle')!.textContent).toContain('3');

    (rows(el)[1].querySelector('.cond-remove') as HTMLButtonElement).click();
    await settle();
    expect(rows(el)).toHaveLength(2);

    for (let i = 0; i < 12; i++) component.addCondition();
    await settle();
    expect(rows(el)).toHaveLength(10);
    expect((el.querySelector('.add-condition') as HTMLButtonElement).disabled).toBe(true);

    // Closing hides the rows; they still count, and come back when opened again
    (el.querySelector('.advanced-toggle') as HTMLButtonElement).click();
    await settle();
    expect(el.querySelector('#advanced-conditions')).toBeNull();
    expect(component.conditions()).toHaveLength(10);
  });

  it('offers the operators of the field\'s type, and choices for a choice field', async () => {
    stubFetch(() => page);
    const { component, el, settle } = await open();
    component.toggleAdvanced();
    await settle();
    TestBed.inject(HttpTestingController).expectOne('/api/voterStatuses').flush(['A', 'V']);
    const row = () => rows(el)[0];

    expect(options(row().querySelector('.cond-op')!)).toEqual(['equals', 'is not', 'starts with', 'ends with', 'contains']);

    choose(row().querySelector('.cond-field')!, 'birthYear');
    await settle();
    expect(options(row().querySelector('.cond-op')!)).toEqual(['equals', 'is not', 'less than', 'at most', 'more than', 'at least', 'between']);
    expect(row().querySelector('.cond-value input')!.getAttribute('type')).toBe('number');

    choose(row().querySelector('.cond-field')!, 'registrationDate');
    await settle();
    expect(options(row().querySelector('.cond-op')!)).toEqual(['on', 'not on', 'before', 'on or before', 'after', 'on or after', 'between']);
    expect(row().querySelector('.cond-value input')!.getAttribute('type')).toBe('date');

    choose(row().querySelector('.cond-field')!, 'gender');
    await settle();
    expect(options(row().querySelector('.cond-op')!)).toEqual(['is', 'is not']);
    expect(options(row().querySelector('.cond-value select')!)).toEqual(['Female', 'Male', 'X']);

    choose(row().querySelector('.cond-field')!, 'voterStatus');
    await settle();
    expect(options(row().querySelector('.cond-value select')!)).toEqual(['A', 'V']);
    expect(component.conditions()[0]).toEqual(expect.objectContaining({ field: 'voterStatus', op: 'is', value: 'A' }));
  });

  it('shows two inputs for "between", and sends both', async () => {
    const fetchMock = stubFetch(() => page);
    const { component, el, settle } = await open();
    component.toggleAdvanced();
    await settle();
    const row = () => rows(el)[0];
    choose(row().querySelector('.cond-field')!, 'birthYear');
    await settle();
    expect(row().querySelectorAll('.cond-value input')).toHaveLength(1);

    choose(row().querySelector('.cond-op')!, 'between');
    await settle();
    const inputs = row().querySelectorAll('.cond-value input');
    expect(inputs).toHaveLength(2);
    expect(Array.from(inputs).map(i => i.getAttribute('aria-label'))).toEqual(['From', 'To']);

    type(inputs[0], '1990');
    type(inputs[1], '1999');
    component.onSubmit();
    await settle();
    expect(votersBody(fetchMock).conditions).toEqual([{ field: 'birthYear', operator: 'between', value: '1990', value2: '1999' }]);
  });

  it('flags the operators that can\'t use an index as maybe slow', async () => {
    stubFetch(() => page);
    const { el, settle } = await open();
    (el.querySelector('.advanced-toggle') as HTMLButtonElement).click();
    await settle();
    const row = () => rows(el)[0];
    expect(row().querySelector('.slow-hint')).toBeNull();

    for (const op of ['contains', 'endsWith', 'notEquals']) {
      choose(row().querySelector('.cond-op')!, op);
      await settle();
      expect(row().querySelector('.slow-hint')?.textContent).toContain('May be slow');
    }
    choose(row().querySelector('.cond-op')!, 'equals');
    await settle();
    expect(row().querySelector('.slow-hint')).toBeNull();
  });

  it('keeps the rows in the URL with Search, and leaves unfinished ones out of the request', async () => {
    const fetchMock = stubFetch(() => page);
    const { component, settle } = await open();
    component.toggleAdvanced();
    const [first] = component.conditions();
    component.setConditionValue(first.id, 'value', 'St. Clair~1');
    component.addCondition();
    component.formGroup.patchValue({ city: 'Testville' });
    component.onSubmit();
    await settle();

    // The value's "." and "~" escaped; the second row has no value yet, but is kept for the page to come back to
    expect(TestBed.inject(Router).url).toBe('/?city=Testville&cond=lastName.startsWith.St%252E%20Clair%257E1~lastName.startsWith.');
    expect(votersBody(fetchMock)).toEqual(expect.objectContaining({
      city: 'Testville',
      conditions: [{ field: 'lastName', operator: 'startsWith', value: 'St. Clair~1' }],
    }));
  });

  it('restores the rows from the URL and searches with them straight away', async () => {
    const fetchMock = stubFetch(() => page);
    const { component, el } = await open('/?lastName=Example&cond=birthYear.between.1990.1999~city.contains.LAN~bogus.eq.1~gender.isNot.M');

    expect(component.advanced()).toBe(true);
    expect(rows(el)).toHaveLength(3);
    expect(votersBody(fetchMock)).toEqual(expect.objectContaining({
      lastName: 'Example',
      conditions: [
        { field: 'birthYear', operator: 'between', value: '1990', value2: '1999' },
        { field: 'city', operator: 'contains', value: 'LAN' },
        { field: 'gender', operator: 'isNot', value: 'M' },
      ],
    }));
    expect((rows(el)[0].querySelectorAll('.cond-value input')[1] as HTMLInputElement).value).toBe('1999');
    expect(rows(el)[1].querySelector('.slow-hint')).not.toBeNull();
  });

  it('exports the same conditions to the CSV', async () => {
    stubFetch(() => page);
    const { el } = await open('/?lastName=Example&cond=birthYear.ge.1990~street.startsWith.100%2520MAIN');

    expect((el.querySelector('a[download]') as HTMLAnchorElement).getAttribute('href')).toBe('Api/VotersCsv?lastName=Example'
      + '&conditions%5B0%5D.field=birthYear&conditions%5B0%5D.operator=ge&conditions%5B0%5D.value=1990'
      + '&conditions%5B1%5D.field=street&conditions%5B1%5D.operator=startsWith&conditions%5B1%5D.value=100+MAIN');
  });

  it('clears the rows, and the URL, with Clear All', async () => {
    const fetchMock = stubFetch(() => page);
    const { component, el, settle } = await open('/?cond=lastName.equals.EXAMPLE');

    component.clearAll();
    await settle();

    expect(component.conditions()).toEqual([]);
    expect(component.advanced()).toBe(false);
    expect(el.querySelector('#advanced-conditions')).toBeNull();
    expect(TestBed.inject(Router).url).toBe('/');
    expect(votersBody(fetchMock)).toBeUndefined();
  });

  describe('a search the API turns down', () => {
    function respondWith(status: number, body: unknown) {
      const fetchMock = vi.fn(async () => new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/problem+json' } }));
      vi.stubGlobal('fetch', fetchMock);
      return fetchMock;
    }

    it('says a search that ran out of time is too broad, rather than showing the last results', async () => {
      stubFetch(() => page);
      const { component, el, settle } = await open();
      expect(el.textContent).toContain('Alex Example');

      respondWith(422, { title: 'Search too broad' });
      component.clicked();
      await settle();

      expect(el.querySelector('.search-problem')!.textContent)
        .toContain('This search is too broad to finish; add a narrower condition (e.g. a city or birth year)');
      expect(el.querySelector('.search-problem')!.getAttribute('role')).toBe('alert');
      expect(el.textContent).not.toContain('Alex Example');
    });

    it('shows the API\'s message for a condition it can\'t use', async () => {
      respondWith(400, { detail: '"abc" is not a valid birthYear: expected a whole number from 1800 to 2200.' });
      const { el } = await open();

      expect(el.querySelector('.search-problem')!.textContent).toContain('"abc" is not a valid birthYear');
    });

    it('falls back to a general message when a 400 has none', async () => {
      vi.stubGlobal('fetch', vi.fn(async () => new Response('nope', { status: 400 })));
      const { el } = await open();

      expect(el.querySelector('.search-problem')!.textContent).toContain('The API could not use this search.');
    });
  });
});
