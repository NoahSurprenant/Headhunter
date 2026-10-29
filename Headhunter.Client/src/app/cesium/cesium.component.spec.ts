import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { fakeCesium, resetFakeCesium } from '../../testing/fake-cesium';
import { areaAddress } from '../../testing/synthetic-data';
import type { AddressDto } from './cesium.component';
import { ClusterDto, clusterId, shortCount, clusterSize } from './clusters';
import type { CesiumComponent as CesiumComponentType } from './cesium.component';

// The real Viewer needs WebGL, which jsdom does not have, so the cesium module is
// replaced with a fake that records what the component does. vi.mock() cannot be
// used here because the Angular builder bundles specs before Vitest sees them, so
// the mock is registered with vi.doMock() and the component is imported afterwards.
const cesium = fakeCesium();
vi.doMock('cesium', () => cesium.module);
let CesiumComponent: typeof CesiumComponentType;

const rad = (deg: number) => (deg * Math.PI) / 180;
const area = (addresses: AddressDto[] = [], clusters: ClusterDto[] = []) => ({ addresses, clusters });

describe('CesiumComponent', () => {
  let http: HttpTestingController;

  beforeAll(async () => {
    ({ CesiumComponent } = await import('./cesium.component'));
  });

  beforeEach(() => {
    resetFakeCesium(cesium);
    cesium.state.rectangle = { west: rad(-85), south: rad(42), east: rad(-84), north: rad(43) };
    TestBed.configureTestingModule({
      imports: [CesiumComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function render() {
    const fixture = TestBed.createComponent(CesiumComponent);
    fixture.detectChanges();
    return { fixture, component: fixture.componentInstance, viewer: cesium.state.viewer! };
  }

  const moveCamera = (viewer: NonNullable<typeof cesium.state.viewer>) => viewer.camera.moveEnd.raise();
  const expectArea = () => http.expectOne(req => req.url === 'api/area');

  it('creates the viewer with the custom imagery picker and flies to Michigan', () => {
    const { viewer } = render();
    expect(viewer.container).toBe('cesiumContainer');
    expect(viewer.options['infoBox']).toBe(false);
    expect(viewer.options['geocoder']).toBe(false);
    const models = viewer.options['imageryProviderViewModels'] as { name: string }[];
    expect(models.length).toBe(4);
    expect((viewer.options['selectedImageryProviderViewModel'] as { name: string }).name).toBe('ArcGIS World Imagery');
    expect(viewer.camera.flyTo).toHaveBeenCalledWith(expect.objectContaining({
      destination: { lon: -84.557916, lat: 43.333202, height: 1000000 },
    }));
  });

  it('loads every base layer from this site\'s /tiles/, with its credit', () => {
    const { viewer } = render();
    const models = viewer.options['imageryProviderViewModels'] as { name: string; creationFunction: () => unknown }[];
    const providers = models.map(m => (m.creationFunction() as { options: { url: string; credit: string } }).options);
    expect(providers.map(p => p.url)).toEqual([
      '/tiles/arcgis-imagery/{z}/{x}/{y}.jpg',
      '/tiles/arcgis-hillshade/{z}/{x}/{y}.jpg',
      '/tiles/esri-ocean/{z}/{x}/{y}.jpg',
      '/tiles/osm/{z}/{x}/{y}.png',
    ]);
    for (const p of providers) {
      expect(p.credit).toBeTruthy();
    }
  });

  it('requests the addresses inside the visible rectangle when the camera stops', () => {
    const { viewer } = render();
    moveCamera(viewer);
    const req = expectArea();
    expect(req.request.method).toBe('GET');
    const params = req.request.params;
    expect(Number(params.get('west'))).toBeCloseTo(-85);
    expect(Number(params.get('east'))).toBeCloseTo(-84);
    expect(Number(params.get('south'))).toBeCloseTo(42);
    expect(Number(params.get('north'))).toBeCloseTo(43);
    req.flush(area([]));
  });

  it('does not query when the camera cannot see the globe', () => {
    const { viewer } = render();
    cesium.state.rectangle = undefined;
    moveCamera(viewer);
    http.expectNone(req => req.url === 'api/area');
  });

  it('adds one labelled point per address, listing its voters in the description', () => {
    const { viewer } = render();
    moveCamera(viewer);
    expectArea().flush(area([areaAddress()]));

    const entity = viewer.entities.getById('addr-0001') as Record<string, any>;
    expect(entity['name']).toBe('123 Fake St');
    expect(entity['position']).toEqual({ lon: -84.5, lat: 43.0, height: 0 });
    expect(entity['label'].text).toBe('123 Fake St');
    expect(entity['description']).toBe(
      '<div>123 Fake St, Testville MI 49999</div><div>Alex Example</div><div>Jordan Sample</div>');
    expect(viewer.entities.suspendEvents).toHaveBeenCalled();
    expect(viewer.entities.resumeEvents).toHaveBeenCalled();
  });

  it('keeps entities that are still visible and removes the ones that are not', () => {
    const { viewer } = render();
    moveCamera(viewer);
    expectArea().flush(area([areaAddress({ id: 'a1' }), areaAddress({ id: 'a2' })]));
    const first = viewer.entities.getById('a1');

    cesium.state.rectangle = { west: rad(-84.9), south: rad(42), east: rad(-84), north: rad(43) };
    moveCamera(viewer);
    expectArea().flush(area([areaAddress({ id: 'a1' }), areaAddress({ id: 'a3' })]));

    expect(viewer.entities.values.map(e => e['id']).sort()).toEqual(['a1', 'a3']);
    expect(viewer.entities.getById('a1')).toBe(first);
    expect(viewer.entities.add).toHaveBeenCalledTimes(3);
  });

  it('ignores a camera stop with the same bounds as the last one', () => {
    const { viewer } = render();
    moveCamera(viewer);
    expectArea().flush(area([]));
    moveCamera(viewer);
    http.expectNone(req => req.url === 'api/area');
  });

  it('cancels the in-flight request when the camera moves again', () => {
    const { viewer } = render();
    moveCamera(viewer);
    const stale = expectArea();

    cesium.state.rectangle = { west: rad(-86), south: rad(41), east: rad(-83), north: rad(44) };
    moveCamera(viewer);
    const current = expectArea();

    expect(stale.cancelled).toBe(true);
    current.flush(area([areaAddress()]));
    expect(viewer.entities.values.length).toBe(1);
  });

  // Cesium invokes the click handler from its own DOM listener, outside anything
  // Angular knows about. With zoneless change detection the info box must still
  // appear and disappear without a manual fixture.detectChanges().
  it('opens the info box for a clicked entity and closes it on an empty click', async () => {
    const { fixture, viewer } = render();
    await fixture.whenStable();
    const entity = { id: 'addr-0001', name: '123 Fake St', description: '<div>Alex Example</div>' };

    cesium.state.picked = { id: entity };
    cesium.state.clickHandler!({ position: {} });
    await fixture.whenStable();
    expect(fixture.componentInstance.selectedEntity()).toBe(entity);
    expect(fixture.nativeElement.querySelector('app-custom-info-box')).not.toBeNull();

    vi.useFakeTimers();
    try {
      cesium.state.picked = undefined;
      cesium.state.clickHandler!({ position: {} });
      await vi.advanceTimersByTimeAsync(300);
    } finally {
      vi.useRealTimers();
    }
    await fixture.whenStable();
    expect(fixture.componentInstance.selectedEntity()).toBeUndefined();
    expect(fixture.nativeElement.querySelector('app-custom-info-box')).toBeNull();
    expect(viewer.trackedEntity).toBeUndefined();
  });

  it('mirrors the viewer tracked entity into a signal', () => {
    const { component, viewer } = render();
    const entity = { id: 'addr-0001' };
    viewer.trackedEntity = entity;
    viewer.trackedEntityChanged.raise();
    expect(component.trackedEntity()).toBe(entity);
  });

  it('draws clusters when zoomed out, sized and labelled by their count', () => {
    const { viewer } = render();
    moveCamera(viewer);
    const c = { latitude: 42.33, longitude: -83.05, count: 45678 };
    expectArea().flush(area([], [c]));

    const entity = viewer.entities.getById(clusterId(c)) as Record<string, any>;
    expect(entity['name']).toBe((45678).toLocaleString() + ' addresses');
    expect(entity['position']).toEqual({ lon: -83.05, lat: 42.33, height: 0 });
    expect(entity['label'].text).toBe('46k');
    expect(entity['point'].pixelSize).toBe(clusterSize(45678, 45678));
  });

  it('sizes clusters against the busiest in view, and draws a lone address without a number', () => {
    const { viewer } = render();
    moveCamera(viewer);
    const big = { latitude: 42.3, longitude: -83.0, count: 10000 };
    const small = { latitude: 42.4, longitude: -83.2, count: 100 };
    const lone = { latitude: 42.5, longitude: -83.4, count: 1 };
    expectArea().flush(area([], [big, small, lone]));

    const size = (c: ClusterDto) => (viewer.entities.getById(clusterId(c)) as Record<string, any>)['point'].pixelSize;
    expect(size(big)).toBe(40);
    expect(size(small)).toBe(17);
    expect(size(lone)).toBe(8);
    expect((viewer.entities.getById(clusterId(lone)) as Record<string, any>)['label']).toBeUndefined();
  });

  it('zooms in on a clicked cluster instead of opening the info box', async () => {
    const { fixture, viewer } = render();
    moveCamera(viewer);
    const c = { latitude: 42.33, longitude: -83.05, count: 5000 };
    expectArea().flush(area([], [c]));
    viewer.camera.flyTo.mockClear();

    cesium.state.picked = { id: viewer.entities.getById(clusterId(c)) };
    cesium.state.clickHandler!({ position: {} });
    await fixture.whenStable();

    expect(viewer.camera.flyTo).toHaveBeenCalledWith({ destination: { lon: -83.05, lat: 42.33, height: 100000 } });
    expect(fixture.componentInstance.selectedEntity()).toBeUndefined();
  });

  it('replaces clusters with addresses when zoomed in far enough', () => {
    const { viewer } = render();
    moveCamera(viewer);
    const c = { latitude: 42.33, longitude: -83.05, count: 5000 };
    expectArea().flush(area([], [c]));

    cesium.state.rectangle = { west: rad(-84.6), south: rad(42.9), east: rad(-84.4), north: rad(43.1) };
    moveCamera(viewer);
    expectArea().flush(area([areaAddress()]));

    expect(viewer.entities.values.map(e => e['id'])).toEqual(['addr-0001']);
  });

  it('shortens cluster counts', () => {
    expect([7, 999, 1000, 1234, 9950, 45678, 999499, 1250000].map(shortCount))
      .toEqual(['7', '999', '1k', '1.2k', '9.9k', '46k', '999k', '1.3M']);
  });
});
