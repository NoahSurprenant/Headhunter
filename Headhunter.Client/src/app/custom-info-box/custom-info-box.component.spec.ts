import { DeferBlockState, TestBed } from '@angular/core/testing';
import type { Entity, Viewer } from 'cesium';
import { createFakeCesium } from '../../testing/fake-cesium';
import { CustomInfoBoxComponent } from './custom-info-box.component';

describe('CustomInfoBoxComponent', () => {
  const { FakeEvent } = createFakeCesium();
  let viewer: { trackedEntity: unknown; trackedEntityChanged: InstanceType<typeof FakeEvent> };
  let entity: { name: string; description: string };

  beforeEach(() => {
    viewer = { trackedEntity: undefined, trackedEntityChanged: new FakeEvent() };
    entity = {
      name: '123 Fake St',
      description: '<div>123 Fake St, Testville MI 49999</div><div>Alex Example</div>',
    };
  });

  afterEach(() => vi.useRealTimers());

  async function render() {
    const fixture = TestBed.createComponent(CustomInfoBoxComponent);
    fixture.componentRef.setInput('viewer', viewer as unknown as Viewer);
    fixture.componentRef.setInput('entity', entity as unknown as Entity);
    fixture.detectChanges();
    const [defer] = await fixture.getDeferBlocks();
    await defer.render(DeferBlockState.Complete);
    const el = fixture.nativeElement as HTMLElement;
    return { fixture, el, box: el.querySelector('.cesium-infoBox') as HTMLElement };
  }

  it('shows the entity name and its HTML description', async () => {
    const { el, box } = await render();
    expect(box.classList).toContain('cesium-infoBox-visible');
    expect(el.querySelector('.cesium-infoBox-title')!.textContent!.trim()).toBe('123 Fake St');
    const description = el.querySelector('.cesium-infoBox-description')!;
    expect(description.querySelectorAll('div').length).toBe(2);
    expect(description.textContent).toContain('Alex Example');
  });

  it('sanitises unsafe markup in the description', async () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
    entity.description = '<div>Alex Example</div><img src="x" onerror="alert(1)"><script>alert(2)</script>';
    const { el } = await render();
    const description = el.querySelector('.cesium-infoBox-description')!;
    expect(description.innerHTML).not.toContain('onerror');
    expect(description.querySelector('script')).toBeNull();
    expect(description.textContent).toContain('Alex Example');
    warn.mockRestore();
  });

  it('toggles camera tracking of the entity', async () => {
    const { fixture, el } = await render();
    const camera = el.querySelector('.cesium-infoBox-camera') as HTMLButtonElement;
    expect(camera.title).toBe('Focus camera on object');

    camera.click();
    await fixture.whenStable();
    expect(viewer.trackedEntity).toBe(entity);
    expect(camera.title).toBe('Stop tracking entity');

    camera.click();
    await fixture.whenStable();
    expect(viewer.trackedEntity).toBeUndefined();
    expect(camera.title).toBe('Focus camera on object');
  });

  it('follows tracking changes made by Cesium itself', async () => {
    const { fixture, el } = await render();
    const camera = el.querySelector('.cesium-infoBox-camera') as HTMLButtonElement;

    // e.g. the user double-clicks the entity or presses Cesium's home button
    viewer.trackedEntity = entity;
    viewer.trackedEntityChanged.raise(entity);
    await fixture.whenStable();
    expect(camera.title).toBe('Stop tracking entity');

    viewer.trackedEntity = undefined;
    viewer.trackedEntityChanged.raise(undefined);
    await fixture.whenStable();
    expect(camera.title).toBe('Focus camera on object');
  });

  it('stops listening to the viewer when destroyed', async () => {
    const { fixture } = await render();
    expect(viewer.trackedEntityChanged.listeners.length).toBe(1);
    fixture.destroy();
    expect(viewer.trackedEntityChanged.listeners.length).toBe(0);
  });

  it('slides out and then emits closeInfoBox after the transition', async () => {
    const { fixture, el, box } = await render();
    vi.useFakeTimers();
    let closed = 0;
    fixture.componentInstance.closeInfoBox.subscribe(() => closed++);

    (el.querySelector('.cesium-infoBox-close') as HTMLButtonElement).click();
    await vi.advanceTimersByTimeAsync(0);
    await fixture.whenStable();
    expect(box.classList).not.toContain('cesium-infoBox-visible');
    expect(closed).toBe(0);

    vi.advanceTimersByTime(300);
    expect(closed).toBe(1);
  });
});
