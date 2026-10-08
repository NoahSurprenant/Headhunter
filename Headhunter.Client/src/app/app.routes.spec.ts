import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Title } from '@angular/platform-browser';
import { Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { createFakeCesium } from '../testing/fake-cesium';
import { stubFetch } from '../testing/fetch-stub';
import { voterDetail } from '../testing/synthetic-data';
import { appConfig } from './app.config';

// /globe lazy-loads CesiumComponent, whose Viewer needs WebGL; swap in the fake
// before any route loads it (see cesium.component.spec.ts).
const cesium = createFakeCesium();
vi.doMock('cesium', () => cesium.module);

describe('app routes (real appConfig)', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [...appConfig.providers, provideHttpClientTesting()],
    });
  });

  afterEach(() => vi.unstubAllGlobals());

  it('redirects the root URL to the globe', async () => {
    const harness = await RouterTestingHarness.create('/');
    expect(TestBed.inject(Router).url).toBe('/globe');
    expect(TestBed.inject(Title).getTitle()).toBe('Globe');
    expect(harness.routeNativeElement?.querySelector('#cesiumContainer')).not.toBeNull();
    expect(cesium.state.viewer?.container).toBe('cesiumContainer');
  });

  it('binds the :id route parameter to the voter page input', async () => {
    const fetchMock = stubFetch(() => voterDetail());
    const harness = await RouterTestingHarness.create('/voter/42');
    await harness.fixture.whenStable();
    harness.detectChanges();

    expect(fetchMock.mock.calls[0][0]).toBe('api/voter/42');
    expect(TestBed.inject(Title).getTitle()).toBe('Voter');
    expect(harness.routeNativeElement?.textContent).toContain('Alex');
  });

  it('binds the :id route parameter to the address page input', async () => {
    const fetchMock = stubFetch(() => [voterDetail()]);
    const harness = await RouterTestingHarness.create('/address/addr-0001');
    await harness.fixture.whenStable();
    harness.detectChanges();

    expect(fetchMock.mock.calls[0][0]).toBe('api/address/addr-0001');
    expect(TestBed.inject(Title).getTitle()).toBe('Address');
    expect(harness.routeNativeElement?.textContent).toContain('1 voters at this address');
  });

  it('serves the voter search page', async () => {
    stubFetch(() => ({ totalCount: 0, results: [] }));
    const harness = await RouterTestingHarness.create('/voters');
    expect(TestBed.inject(Title).getTitle()).toBe('Voters');
    expect(harness.routeNativeElement?.textContent).toContain('Search Filters');
  });
});
