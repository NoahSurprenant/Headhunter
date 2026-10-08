import { vi } from 'vitest';

// A minimal stand-in for the parts of the cesium module CesiumComponent uses.
// The real Viewer needs WebGL, which jsdom does not have.
export function createFakeCesium() {
  class FakeEvent {
    listeners: { fn: (...args: unknown[]) => void; scope?: unknown }[] = [];
    // Like Cesium's Event, returns a function that removes the listener.
    addEventListener(fn: (...args: unknown[]) => void, scope?: unknown) {
      const listener = { fn, scope };
      this.listeners.push(listener);
      return () => { this.listeners = this.listeners.filter(l => l !== listener); };
    }
    raise(...args: unknown[]) { this.listeners.forEach(l => l.fn.apply(l.scope, args)); }
  }

  class FakeEntityCollection {
    private byId = new Map<string, Record<string, unknown>>();
    suspendEvents = vi.fn();
    resumeEvents = vi.fn();
    get values() { return [...this.byId.values()]; }
    getById(id: string) { return this.byId.get(id); }
    removeById(id: string) { return this.byId.delete(id); }
    add = vi.fn((entity: Record<string, unknown>) => {
      this.byId.set(entity['id'] as string, entity);
      return entity;
    });
  }

  const state = {
    viewer: undefined as FakeViewer | undefined,
    clickHandler: undefined as ((movement: { position: unknown }) => void) | undefined,
    rectangle: { west: 0, south: 0, east: 0, north: 0 } as { west: number; south: number; east: number; north: number } | undefined,
    picked: undefined as { id?: unknown } | undefined,
  };

  class FakeViewer {
    entities = new FakeEntityCollection();
    trackedEntity: unknown = undefined;
    trackedEntityChanged = new FakeEvent();
    camera = {
      flyTo: vi.fn(),
      moveEnd: new FakeEvent(),
      computeViewRectangle: vi.fn(() => state.rectangle),
    };
    scene = { canvas: {}, globe: { ellipsoid: {} }, pick: vi.fn(() => state.picked) };
    constructor(public container: string, public options: Record<string, unknown>) {
      state.viewer = this;
    }
  }

  const degrees = (radians: number) => (radians * 180) / Math.PI;

  return {
    state,
    FakeEvent,
    module: {
      Viewer: FakeViewer,
      ScreenSpaceEventHandler: class {
        setInputAction(fn: (movement: { position: unknown }) => void) { state.clickHandler = fn; }
      },
      ScreenSpaceEventType: { LEFT_CLICK: 'LEFT_CLICK' },
      ArcGisMapService: { defaultAccessToken: undefined as string | undefined },
      ProviderViewModel: class { name: string; constructor(options: { name: string }) { this.name = options.name; } },
      ArcGisMapServerImageryProvider: { fromBasemapType: vi.fn() },
      ArcGisBaseMapType: { SATELLITE: 1, HILLSHADE: 2, OCEANS: 3 },
      OpenStreetMapImageryProvider: class {},
      buildModuleUrl: (path: string) => `/assets/cesium/${path}`,
      Cartesian2: class { constructor(public x: number, public y: number) {} },
      Cartesian3: { fromDegrees: (lon: number, lat: number, height = 0) => ({ lon, lat, height }) },
      Math: { toRadians: (deg: number) => (deg * Math.PI) / 180, toDegrees: degrees },
      Color: { AQUA: 'AQUA', WHITE: 'WHITE' },
      LabelStyle: { FILL_AND_OUTLINE: 'FILL_AND_OUTLINE' },
      VerticalOrigin: { TOP: 'TOP' },
      defined: (value: unknown) => value !== undefined && value !== null,
    },
  };
}
