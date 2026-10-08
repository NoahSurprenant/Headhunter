import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { createFakeCesium } from '../../testing/fake-cesium';
import { areaAddress } from '../../testing/synthetic-data';
import type { CesiumComponent as CesiumComponentType } from './cesium.component';

// The real Viewer needs WebGL, which jsdom does not have, so the cesium module is
// replaced with a fake that records what the component does. vi.mock() cannot be
// used here because the Angular builder bundles specs before Vitest sees them, so
// the mock is registered with vi.doMock() and the component is imported afterwards.
const cesium = createFakeCesium();
vi.doMock('cesium', () => cesium.module);
let CesiumComponent: typeof CesiumComponentType;

const rad = (deg: number) => (deg * Math.PI) / 180;

describe('CesiumComponent', () => {
  let http: HttpTestingController;

  beforeAll(async () => {
    ({ CesiumComponent } = await import('./cesium.component'));
  });

  beforeEach(() => {
    cesium.state.viewer = undefined;
    cesium.state.picked = undefined;
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
    expect(models.length).toBe(8);
    expect((viewer.options['selectedImageryProviderViewModel'] as { name: string }).name).toBe('ArcGIS World Imagery');
    expect(viewer.camera.flyTo).toHaveBeenCalledWith(expect.objectContaining({
      destination: { lon: -84.557916, lat: 43.333202, height: 1000000 },
    }));
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
    req.flush([]);
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
    expectArea().flush([areaAddress()]);

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
    expectArea().flush([areaAddress({ id: 'a1' }), areaAddress({ id: 'a2' })]);
    const first = viewer.entities.getById('a1');

    cesium.state.rectangle = { west: rad(-84.9), south: rad(42), east: rad(-84), north: rad(43) };
    moveCamera(viewer);
    expectArea().flush([areaAddress({ id: 'a1' }), areaAddress({ id: 'a3' })]);

    expect(viewer.entities.values.map(e => e['id']).sort()).toEqual(['a1', 'a3']);
    expect(viewer.entities.getById('a1')).toBe(first);
    expect(viewer.entities.add).toHaveBeenCalledTimes(3);
  });

  it('ignores a camera stop with the same bounds as the last one', () => {
    const { viewer } = render();
    moveCamera(viewer);
    expectArea().flush([]);
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
    current.flush([areaAddress()]);
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
});
