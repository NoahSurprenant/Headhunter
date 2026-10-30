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
    removeAll = vi.fn(() => this.byId.clear());
    add = vi.fn((entity: Record<string, unknown>) => {
      this.byId.set(entity['id'] as string, entity);
      return entity;
    });
  }

  const degrees = (radians: number) => (radians * 180) / Math.PI;
  const radians = (deg: number) => (deg * Math.PI) / 180;

  const state = {
    viewer: undefined as FakeViewer | undefined,
    clickHandler: undefined as ((movement: { position: unknown }) => void) | undefined,
    // Every ScreenSpaceEventHandler action by event type (LEFT_DOWN, MOUSE_MOVE, LEFT_UP for drawing an area).
    handlers: {} as Record<string, (movement: Record<string, unknown>) => void>,
    // The MOUSE_MOVE action again, typed for the cluster tooltip specs.
    moveHandler: undefined as ((movement: { endPosition: { x: number; y: number } }) => void) | undefined,
    rectangle: { west: 0, south: 0, east: 0, north: 0 } as { west: number; south: number; east: number; north: number } | undefined,
    picked: undefined as { id?: unknown } | undefined,
    // Where the (fake) camera currently sits, read by persistViewToUrl; tests move it by assignment,
    // like state.rectangle, before raising moveEnd.
    view: { latitude: radians(43.333202), longitude: radians(-84.557916), height: 400000, heading: 0, pitch: radians(-90) },
  };

  class FakeViewer {
    entities = new FakeEntityCollection();
    trackedEntity: unknown = undefined;
    trackedEntityChanged = new FakeEvent();
    camera = {
      flyTo: vi.fn(),
      flyToBoundingSphere: vi.fn(),
      setView: vi.fn(),
      get positionCartographic() { return { height: state.view.height, latitude: state.view.latitude, longitude: state.view.longitude }; },
      get heading() { return state.view.heading; },
      get pitch() { return state.view.pitch; },
      moveEnd: new FakeEvent(),
      computeViewRectangle: vi.fn(() => state.rectangle),
      // Screen positions in specs are { x: longitude, y: latitude }; anything else is off the globe.
      pickEllipsoid: vi.fn((position: { x?: number; y?: number } | undefined) =>
        position?.x === undefined ? undefined : { lon: position.x, lat: position.y!, height: 0 }),
    };
    scene = {
      canvas: {},
      globe: { ellipsoid: {} },
      pick: vi.fn(() => state.picked),
      screenSpaceCameraController: { enableInputs: true },
    };
    constructor(public container: string, public options: Record<string, unknown>) {
      state.viewer = this;
    }
  }

  return {
    state,
    FakeEvent,
    module: {
      Viewer: FakeViewer,
      ScreenSpaceEventHandler: class {
        setInputAction(fn: (movement: never) => void, type: string) {
          state.handlers[type] = fn as (movement: Record<string, unknown>) => void;
          if (type === 'LEFT_CLICK') state.clickHandler = fn as typeof state.clickHandler;
          if (type === 'MOUSE_MOVE') state.moveHandler = fn as typeof state.moveHandler;
        }
      },
      ScreenSpaceEventType: { LEFT_CLICK: 'LEFT_CLICK', LEFT_DOWN: 'LEFT_DOWN', MOUSE_MOVE: 'MOUSE_MOVE', LEFT_UP: 'LEFT_UP' },
      ProviderViewModel: class {
        name: string;
        creationFunction: () => unknown;
        constructor(options: { name: string; creationFunction: () => unknown }) {
          this.name = options.name;
          this.creationFunction = options.creationFunction;
        }
      },
      UrlTemplateImageryProvider: class { constructor(public options: { url: string; maximumLevel: number; credit: string }) {} },
      buildModuleUrl: (path: string) => `/assets/cesium/${path}`,
      Cartesian2: class { constructor(public x: number, public y: number) {} },
      Cartesian3: class {
        constructor(public x: number, public y: number, public z: number) {}
        static fromDegrees(lon: number, lat: number, height = 0) { return { lon, lat, height }; }
      },
      Cartographic: { fromCartesian: (c: { lon: number; lat: number }) => ({ longitude: (c.lon * Math.PI) / 180, latitude: (c.lat * Math.PI) / 180 }) },
      Rectangle: { fromDegrees: (west: number, south: number, east: number, north: number) => ({ west, south, east, north }) },
      BoundingSphere: { fromRectangle3D: (rectangle: object) => ({ sphereAround: rectangle }) },
      HeadingPitchRange: class { constructor(public heading: number, public pitch: number, public range: number) {} },
      Math: { toRadians: radians, toDegrees: degrees },
      Color: {
        AQUA: 'AQUA', WHITE: Object.assign(new String('WHITE'), { withAlpha: (a: number) => `WHITE@${a}` }), BLACK: 'BLACK',
        YELLOW: { name: 'YELLOW', withAlpha: (a: number) => `YELLOW@${a}` },
        ORANGE: { withAlpha: (a: number) => `ORANGE@${a}` },
        GRAY: { withAlpha: (a: number) => `GRAY@${a}` },
        fromCssColorString: (css: string) => ({ withAlpha: (a: number) => `${css}@${a}` }),
        GOLD: { toString: () => 'GOLD', withAlpha: (a: number) => `GOLD@${a}` },
      },
      ConstantProperty: class { constructor(public value: unknown) {} },
      LabelStyle: { FILL_AND_OUTLINE: 'FILL_AND_OUTLINE' },
      VerticalOrigin: { TOP: 'TOP', CENTER: 'CENTER' },
      HorizontalOrigin: { LEFT: 'LEFT', CENTER: 'CENTER' },
      DistanceDisplayCondition: class { constructor(public near: number, public far: number) {} },
      defined: (value: unknown) => value !== undefined && value !== null,
    },
  };
}

type FakeCesium = ReturnType<typeof createFakeCesium>;

/**
 * The fake shared by every spec in a test worker. Specs can run in one worker
 * without module isolation, and the first vi.doMock('cesium') factory used is
 * the one CesiumComponent keeps, so all specs must hand out the same object.
 * Call resetFakeCesium() in beforeEach.
 */
export function fakeCesium(): FakeCesium {
  const holder = globalThis as { __headhunterFakeCesium?: FakeCesium };
  return (holder.__headhunterFakeCesium ??= createFakeCesium());
}

export function resetFakeCesium(fake: FakeCesium) {
  fake.state.viewer = undefined;
  fake.state.clickHandler = undefined;
  fake.state.handlers = {};
  fake.state.moveHandler = undefined;
  fake.state.picked = undefined;
  fake.state.rectangle = undefined;
  fake.state.view = { latitude: (43.333202 * Math.PI) / 180, longitude: (-84.557916 * Math.PI) / 180, height: 400000, heading: 0, pitch: (-90 * Math.PI) / 180 };
}
