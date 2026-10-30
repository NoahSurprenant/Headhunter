import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
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
  let navigate: ReturnType<typeof vi.spyOn>;

  beforeAll(async () => {
    ({ CesiumComponent } = await import('./cesium.component'));
  });

  beforeEach(() => {
    resetFakeCesium(cesium);
    cesium.state.rectangle = { west: rad(-85), south: rad(42), east: rad(-84), north: rad(43) };
    TestBed.configureTestingModule({
      imports: [CesiumComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    http = TestBed.inject(HttpTestingController);
    // persistViewToUrl calls this on every camera stop; stub it so tests don't perform a real
    // navigation, and so they can assert on what it was asked to do.
    navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
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
      '<div><a href="/address/addr-0001">123 Fake St</a>, Testville MI 49999</div>'
      + '<div><a href="/voter/voter-0001">Alex Example</a></div><div><a href="/voter/voter-0002">Jordan Sample</a></div>');
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

  it('starts at the place in its inputs (a /globe?lat&lon&address link) and opens that address once it loads', () => {
    const fixture = TestBed.createComponent(CesiumComponent);
    fixture.componentRef.setInput('lat', '42.3');
    fixture.componentRef.setInput('lon', '-83.1');
    fixture.componentRef.setInput('address', 'addr-0001');
    fixture.detectChanges();
    const viewer = cesium.state.viewer!;

    expect(viewer.camera.flyTo).toHaveBeenCalledTimes(1);
    expect(viewer.camera.flyTo.mock.calls[0][0].destination).toEqual({ lon: -83.1, lat: 42.3, height: 350 });

    moveCamera(viewer);
    expectArea().flush(area([areaAddress()]));
    expect(fixture.componentInstance.selectedEntity()).toBe(viewer.entities.getById('addr-0001'));
  });

  it('flies to a new place when its inputs change', async () => {
    const { fixture, viewer } = render();
    viewer.camera.flyTo.mockClear();

    fixture.componentRef.setInput('lat', 42.5);
    fixture.componentRef.setInput('lon', -83.5);
    await fixture.whenStable();

    expect(viewer.camera.flyTo).toHaveBeenCalledWith(expect.objectContaining({ destination: { lon: -83.5, lat: 42.5, height: 350 } }));
  });

  it('restores a bookmarked view (lat/lon/h, no address) instantly instead of flying', () => {
    const fixture = TestBed.createComponent(CesiumComponent);
    fixture.componentRef.setInput('lat', 42.3);
    fixture.componentRef.setInput('lon', -83.1);
    fixture.componentRef.setInput('h', 1200);
    fixture.componentRef.setInput('heading', 10);
    fixture.componentRef.setInput('pitch', -45);
    fixture.detectChanges();
    const viewer = cesium.state.viewer!;

    expect(viewer.camera.flyTo).not.toHaveBeenCalled();
    expect(viewer.camera.setView).toHaveBeenCalledWith({
      destination: { lon: -83.1, lat: 42.3, height: 1200 },
      orientation: { heading: rad(10), pitch: rad(-45) },
    });
  });

  it('defaults heading/pitch (straight down, north up) when a bookmarked view omits them', () => {
    const fixture = TestBed.createComponent(CesiumComponent);
    fixture.componentRef.setInput('lat', 42.3);
    fixture.componentRef.setInput('lon', -83.1);
    fixture.componentRef.setInput('h', 1200);
    fixture.detectChanges();
    const viewer = cesium.state.viewer!;

    expect(viewer.camera.setView).toHaveBeenCalledWith({
      destination: { lon: -83.1, lat: 42.3, height: 1200 },
      orientation: { heading: rad(0), pitch: rad(-90) },
    });
  });

  it('writes the settled camera view to the URL (rounded, address cleared) after the camera stops', () => {
    const { viewer } = render();
    cesium.state.view = { latitude: rad(42.5), longitude: rad(-83.25), height: 5000, heading: rad(12.4), pitch: rad(-60.6) };

    moveCamera(viewer);
    expectArea().flush(area([]));

    expect(navigate).toHaveBeenCalledWith([], expect.objectContaining({
      queryParams: { lat: 42.5, lon: -83.25, h: 5000, heading: 12, pitch: -61, address: null },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    }));
  });

  it('does not re-write the URL for a camera stop with the same (rounded) view as the last one', () => {
    const { viewer } = render();
    cesium.state.view = { latitude: rad(42.5), longitude: rad(-83.25), height: 5000, heading: 0, pitch: rad(-90) };

    moveCamera(viewer);
    expectArea().flush(area([]));
    navigate.mockClear();

    // Same rectangle too, so computeBounds also skips its request (covered by the "ignores a camera
    // stop with the same bounds" test above); nothing left to flush here.
    moveCamera(viewer);
    http.expectNone(req => req.url === 'api/area');

    expect(navigate).not.toHaveBeenCalled();
  });

  it('does not loop: feeding the just-written view back in as inputs does not setView again', () => {
    const { fixture, viewer } = render();
    cesium.state.view = { latitude: rad(42.5), longitude: rad(-83.25), height: 5000, heading: rad(12), pitch: rad(-60) };

    moveCamera(viewer);
    expectArea().flush(area([]));
    viewer.camera.setView.mockClear();

    // What withComponentInputBinding would do once the URL above is live: feed the same (rounded)
    // view straight back in as inputs.
    fixture.componentRef.setInput('lat', 42.5);
    fixture.componentRef.setInput('lon', -83.25);
    fixture.componentRef.setInput('h', 5000);
    fixture.componentRef.setInput('heading', 12);
    fixture.componentRef.setInput('pitch', -60);

    expect(viewer.camera.setView).not.toHaveBeenCalled();
  });

  it('copies the current URL and shows a brief "Copied" toast', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true });
    const { component } = render();

    vi.useFakeTimers();
    try {
      component.copyLink();
      await vi.advanceTimersByTimeAsync(0);
      expect(writeText).toHaveBeenCalledWith(window.location.href);
      expect(component.copied()).toBe(true);

      await vi.advanceTimersByTimeAsync(1500);
      expect(component.copied()).toBe(false);
    } finally {
      vi.useRealTimers();
    }
  });

  it('escapes names in the info box description', () => {
    const { viewer } = render();
    moveCamera(viewer);
    expectArea().flush(area([areaAddress({ voters: [{ id: 'v<1', firstName: '<b>Al</b>', lastName: 'O\'Brien' }] })]));
    const description = (viewer.entities.getById('addr-0001') as Record<string, any>)['description'] as string;
    expect(description).toContain('<a href="/voter/v%3C1">&lt;b&gt;Al&lt;/b&gt; O&#39;Brien</a>');
  });

  describe('Select area', () => {
    // Screen positions in the fake are { x: longitude, y: latitude }; {} is off the globe.
    const at = (longitude: number, latitude: number) => ({ x: longitude, y: latitude });
    const down = (position: object) => cesium.state.handlers['LEFT_DOWN']({ position });
    const move = (endPosition: object) => cesium.state.handlers['MOUSE_MOVE']({ endPosition });
    const up = (position: object) => cesium.state.handlers['LEFT_UP']({ position });
    const areaEntity = (viewer: NonNullable<typeof cesium.state.viewer>) =>
      viewer.entities.getById('selected-area') as Record<string, any> | undefined;

    async function renderSelectable() {
      const fixture = TestBed.createComponent(CesiumComponent);
      fixture.componentRef.setInput('selectable', true);
      await fixture.whenStable();
      const emitted: unknown[] = [];
      fixture.componentInstance.areaSelected.subscribe(box => emitted.push(box));
      const toggle = () => (fixture.nativeElement.querySelector('button.area-toggle') as HTMLButtonElement);
      return { fixture, component: fixture.componentInstance, viewer: cesium.state.viewer!, emitted, toggle };
    }

    it('has no toggle unless the page asks for one', async () => {
      const { fixture } = render();
      await fixture.whenStable();
      expect(fixture.nativeElement.querySelector('button.area-toggle')).toBeNull();
    });

    it('sits beside "Copy link" in the one top-left toolbar', async () => {
      const { fixture } = await renderSelectable();
      const toolbar = fixture.nativeElement.querySelector('.globe-toolbar') as HTMLElement;
      expect([...toolbar.querySelectorAll('button')].map(b => b.textContent!.trim()))
        .toEqual(['Copy link', 'Select area']);
    });

    it('stops the camera moving while the toggle is on, and starts it again when it goes off', async () => {
      const { fixture, viewer, toggle } = await renderSelectable();
      expect(toggle().textContent).toContain('Select area');

      toggle().click();
      await fixture.whenStable();
      expect(viewer.scene.screenSpaceCameraController.enableInputs).toBe(false);
      expect(toggle().getAttribute('aria-pressed')).toBe('true');
      expect(toggle().textContent).toContain('Drag on the globe');

      toggle().click();
      await fixture.whenStable();
      expect(viewer.scene.screenSpaceCameraController.enableInputs).toBe(true);
    });

    it('ignores a drag while the toggle is off', async () => {
      const { viewer, emitted } = await renderSelectable();
      down(at(-84, 43));
      move(at(-83, 42));
      up(at(-83, 42));
      expect(areaEntity(viewer)).toBeUndefined();
      expect(emitted).toEqual([]);
    });

    it('draws the box while dragging and hands it to the page when let go', async () => {
      const { fixture, component, viewer, emitted } = await renderSelectable();
      component.toggleDrawing();

      down(at(-84, 43));
      move(at(-83.5, 42.5));
      const entity = areaEntity(viewer)!;
      expect(entity['rectangle'].coordinates).toEqual({ west: -84, south: 42.5, east: -83.5, north: 43 });
      expect(entity['rectangle'].material).toBe('YELLOW@0.2');

      move({}); // Over space: the box stays as it was.
      move(at(-83, 42));
      expect(areaEntity(viewer)).toBe(entity);
      expect(entity['rectangle'].coordinates).toEqual({ west: -84, south: 42, east: -83, north: 43 });

      up(at(-83, 42));
      await fixture.whenStable();
      expect(emitted).toEqual([{ west: -84, east: -83, north: 43, south: 42 }]);
      expect(component.drawing()).toBe(false);
      expect(viewer.scene.screenSpaceCameraController.enableInputs).toBe(true);
    });

    it('selects a box across the date line', async () => {
      const { component, emitted } = await renderSelectable();
      component.toggleDrawing();
      down(at(179, 11));
      up(at(-179, 9));
      expect(emitted).toEqual([{ west: 179, east: -179, north: 11, south: 9 }]);
    });

    it('selects nothing for a click without a drag, and that click opens nothing', async () => {
      const { fixture, component, viewer, emitted } = await renderSelectable();
      component.toggleDrawing();
      down(at(-84, 43));
      up(at(-84, 43));
      cesium.state.picked = { id: { id: 'addr-0001' } };
      cesium.state.clickHandler!({ position: at(-84, 43) });
      await fixture.whenStable();

      expect(emitted).toEqual([]);
      expect(areaEntity(viewer)).toBeUndefined();
      expect(component.selectedEntity()).toBeUndefined();
      expect(component.drawing()).toBe(false);

      // Later clicks work again.
      await new Promise(resolve => setTimeout(resolve));
      cesium.state.clickHandler!({ position: at(-84, 43) });
      expect(component.selectedEntity()).toEqual({ id: 'addr-0001' });
    });

    it('does not start a box off the globe', async () => {
      const { component, viewer, emitted } = await renderSelectable();
      component.toggleDrawing();
      down({});
      move(at(-83, 42));
      up(at(-83, 42));
      expect(areaEntity(viewer)).toBeUndefined();
      expect(emitted).toEqual([]);
      expect(component.drawing()).toBe(true);
    });

    it('puts the selected area back when drawing is switched off mid-drag', async () => {
      const { fixture, component, viewer } = await renderSelectable();
      fixture.componentRef.setInput('selectedArea', { west: -84, east: -83, north: 43, south: 42 });
      await fixture.whenStable();
      component.toggleDrawing();
      down(at(-90, 40));
      move(at(-89, 41));
      component.toggleDrawing();
      expect(areaEntity(viewer)!['rectangle'].coordinates).toEqual({ west: -84, south: 42, east: -83, north: 43 });
      move(at(-88, 39)); // No longer dragging.
      expect(areaEntity(viewer)!['rectangle'].coordinates).toEqual({ west: -84, south: 42, east: -83, north: 43 });
    });

    it('shows the selected area, keeps it when the view refreshes, and removes it when cleared', async () => {
      const { fixture, viewer } = await renderSelectable();
      fixture.componentRef.setInput('selectedArea', { west: -84, east: -83, north: 43, south: 42 });
      await fixture.whenStable();
      expect(areaEntity(viewer)!['rectangle'].coordinates).toEqual({ west: -84, south: 42, east: -83, north: 43 });

      moveCamera(viewer);
      expectArea().flush(area([areaAddress()]));
      expect(viewer.entities.values.map(e => e['id']).sort()).toEqual(['addr-0001', 'selected-area']);

      fixture.componentRef.setInput('selectedArea', undefined);
      await fixture.whenStable();
      expect(areaEntity(viewer)).toBeUndefined();
    });

    it('starts at the selected area (a reloaded page) when there is no place to show', () => {
      const fixture = TestBed.createComponent(CesiumComponent);
      fixture.componentRef.setInput('selectedArea', { west: -84, east: -83, north: 43, south: 42 });
      fixture.detectChanges();
      const viewer = cesium.state.viewer!;
      expect(viewer.camera.flyTo).not.toHaveBeenCalled();
      expect(viewer.camera.flyToBoundingSphere).toHaveBeenCalledWith(
        { sphereAround: { west: -84, south: 42, east: -83, north: 43 } },
        { offset: expect.objectContaining({ heading: 0, pitch: -Math.PI / 2, range: 0 }) });
      expect(areaEntity(viewer)).toBeDefined();
    });

    it('treats a click on the selected area as a click on the globe', async () => {
      const { fixture, component, viewer } = await renderSelectable();
      fixture.componentRef.setInput('selectedArea', { west: -84, east: -83, north: 43, south: 42 });
      await fixture.whenStable();
      cesium.state.picked = { id: areaEntity(viewer) };
      cesium.state.clickHandler!({ position: at(-83.5, 42.5) });
      expect(component.selectedEntity()).toBeUndefined();
    });
  });

  it('gives Cesium\'s widget icons an empty alt, and keeps an alt that is there', async () => {
    const { markDecorative } = await import('./cesium.component');
    const root = document.createElement('div');
    root.innerHTML = '<img src="a.svg"><img src="b.png" alt="Imagery">';
    markDecorative(root);
    expect(Array.from(root.querySelectorAll('img')).map(i => i.getAttribute('alt'))).toEqual(['', 'Imagery']);
    markDecorative(null); // no container: nothing to do
  });

  describe('with a district', () => {
    const expectExtent = () => http.expectOne(req => req.url === 'api/districtExtent');
    const precinct = { districtType: 'Precinct', district: '00012', districtWithin: 'CITY OF TESTVILLE' };

    function renderDistrict(inputs: Record<string, string>) {
      const fixture = TestBed.createComponent(CesiumComponent);
      for (const [name, value] of Object.entries(inputs)) fixture.componentRef.setInput(name, value);
      fixture.detectChanges();
      return { fixture, viewer: cesium.state.viewer! };
    }

    it('starts over the district (a /globe?districtType&district link), with a margin around its extent', () => {
      const { viewer } = renderDistrict(precinct);

      // Not Michigan first: straight to the district once its extent arrives.
      expect(viewer.camera.flyTo).not.toHaveBeenCalled();
      const req = expectExtent();
      expect(req.request.params.get('type')).toBe('Precinct');
      expect(req.request.params.get('name')).toBe('00012');
      expect(req.request.params.get('within')).toBe('CITY OF TESTVILLE');
      req.flush({ west: -84, east: -83, south: 42, north: 43 });

      const destination = viewer.camera.flyTo.mock.calls[0][0].destination;
      expect(destination.west).toBeCloseTo(-84.1);
      expect(destination.east).toBeCloseTo(-82.9);
      expect(destination.south).toBeCloseTo(41.9);
      expect(destination.north).toBeCloseTo(43.1);
    });

    it('keeps a district of one address a few streets across', () => {
      const { viewer } = renderDistrict({ districtType: 'County', district: 'TESTSHIRE' });
      expectExtent().flush({ west: -84, east: -84, south: 42, north: 42 });

      const destination = viewer.camera.flyTo.mock.calls[0][0].destination;
      expect(destination.east - destination.west).toBeCloseTo(0.01);
    });

    it('asks only for the district\'s addresses and draws them and its clusters in gold', () => {
      const { viewer } = renderDistrict(precinct);
      expectExtent().flush({ west: -84, east: -83, south: 42, north: 43 });

      moveCamera(viewer);
      const req = expectArea();
      expect(req.request.params.get('districtType')).toBe('Precinct');
      expect(req.request.params.get('district')).toBe('00012');
      expect(req.request.params.get('districtWithin')).toBe('CITY OF TESTVILLE');
      req.flush(area([areaAddress()], [{ latitude: 42.5, longitude: -83.5, count: 12 }]));

      expect((viewer.entities.getById('addr-0001') as Record<string, any>)['point'].color).toBe(cesium.module.Color.GOLD);
      const cluster = viewer.entities.values.find(e => String(e['id']).startsWith('cluster:')) as Record<string, any>;
      expect(cluster['point'].color).toBe('GOLD@0.75');
    });

    it('clears the district\'s points and asks for everyone\'s again when the district is cleared', async () => {
      const { fixture, viewer } = renderDistrict(precinct);
      expectExtent().flush({ west: -84, east: -83, south: 42, north: 43 });
      cesium.state.rectangle = { west: rad(-85), south: rad(42), east: rad(-84), north: rad(43) };
      moveCamera(viewer);
      expectArea().flush(area([areaAddress()]));

      fixture.componentRef.setInput('district', undefined);
      await fixture.whenStable();

      expect(viewer.entities.getById('addr-0001')).toBeUndefined();
      // Same view, no district: asked again, unfiltered.
      const req = expectArea();
      expect(req.request.params.has('districtType')).toBe(false);
      req.flush(area([areaAddress()]));
      expect((viewer.entities.getById('addr-0001') as Record<string, any>)['point'].color).toBe('AQUA');
    });

    it('flies to a district picked after it started', async () => {
      const { fixture, viewer } = render();
      viewer.camera.flyTo.mockClear();

      fixture.componentRef.setInput('districtType', 'County');
      fixture.componentRef.setInput('district', 'TESTSHIRE');
      await fixture.whenStable();
      expectExtent().flush({ west: -85, east: -84, south: 42, north: 43 });

      expect(viewer.camera.flyTo).toHaveBeenCalledTimes(1);
    });

    it('stays put and shows what it can when the district is not on the map', () => {
      cesium.state.rectangle = { west: rad(-85), south: rad(42), east: rad(-84), north: rad(43) };
      const { viewer } = renderDistrict({ districtType: 'County', district: 'NOWHERE' });
      expectExtent().flush('Not found', { status: 404, statusText: 'Not Found' });

      expect(viewer.camera.flyTo).not.toHaveBeenCalled();
      expect(expectArea().request.params.get('district')).toBe('NOWHERE');
    });

    it('keeps the selected area outlined when the district changes', async () => {
      const { fixture, viewer } = renderDistrict(precinct);
      fixture.componentRef.setInput('selectedArea', { west: -84, east: -83, north: 43, south: 42 });
      await fixture.whenStable();
      expectExtent().flush({ west: -84, east: -83, south: 42, north: 43 });

      fixture.componentRef.setInput('district', undefined);
      await fixture.whenStable();
      expectArea().flush(area([]));

      expect(viewer.entities.getById('selected-area')).toBeDefined();
    });

    it('ignores a precinct without its jurisdiction', () => {
      const { viewer } = renderDistrict({ districtType: 'Precinct', district: '00012' });
      http.expectNone(req => req.url === 'api/districtExtent');
      expect(viewer.camera.flyTo).toHaveBeenCalledTimes(1); // Michigan
    });
  });
});
