import { ChangeDetectionStrategy, Component, computed, effect, inject, input, output, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Entity, Viewer } from 'cesium';

@Component({
  selector: 'app-custom-info-box',
  templateUrl: './custom-info-box.component.html',
  styleUrls: ['./custom-info-box.component.css'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CustomInfoBoxComponent {
  viewer = input.required<Viewer>();
  entity = input.required<Entity>();
  closeInfoBox = output<void>();
  // While closing, the cesium-infoBox-visible class is removed and Cesium's own
  // widget CSS slides the box out; the slide-in is the animate.enter keyframes.
  closing = signal(false);

  // Mirror of viewer.trackedEntity. Cesium can change it on its own (e.g. the
  // home button), so follow trackedEntityChanged instead of reading the viewer.
  private trackedEntity = signal<Entity | undefined>(undefined);
  isTracking = computed(() => this.trackedEntity() === this.entity());

  constructor() {
    effect((onCleanup) => {
      const viewer = this.viewer();
      this.trackedEntity.set(viewer.trackedEntity);
      const removeListener = viewer.trackedEntityChanged.addEventListener(
        () => this.trackedEntity.set(viewer.trackedEntity));
      onCleanup(removeListener);
    });
  }

  track(): void {
    const viewer = this.viewer();
    viewer.trackedEntity = this.isTracking() ? undefined : this.entity();
    this.trackedEntity.set(viewer.trackedEntity);
  }

  private router = inject(Router);

  /** The description's links (/address/.., /voter/..) open inside the app, not with a page load. */
  followLink(event: MouseEvent): void {
    const link = (event.target as HTMLElement | null)?.closest('a');
    const href = link?.getAttribute('href');
    if (href?.startsWith('/') && !event.ctrlKey && !event.metaKey && !event.shiftKey && event.button === 0) {
      event.preventDefault();
      this.router.navigateByUrl(href);
    }
  }

  close(): void {
    this.closing.set(true);
    setTimeout(() => this.closeInfoBox.emit(), 300);
  }
}
