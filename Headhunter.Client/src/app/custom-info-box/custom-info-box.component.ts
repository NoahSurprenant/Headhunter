import { ChangeDetectionStrategy, Component, input, output, signal } from '@angular/core';
import { Entity, Viewer } from 'cesium';

@Component({
  selector: 'app-custom-info-box',
  templateUrl: './custom-info-box.component.html',
  styleUrls: ['./custom-info-box.component.css'],
  // Eager: isTracking() reads the mutable Cesium viewer.trackedEntity, which does
  // not notify OnPush when the tracked entity changes.
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class CustomInfoBoxComponent {
  viewer = input.required<Viewer>();
  entity = input.required<Entity>();
  closeInfoBox = output<void>();
  // While closing, the cesium-infoBox-visible class is removed and Cesium's own
  // widget CSS slides the box out; the slide-in is the animate.enter keyframes.
  closing = signal(false);

  isTracking(): boolean {
    return this.viewer().trackedEntity === this.entity();
  }

  track(): void {
    if (!this.isTracking())
      this.viewer().trackedEntity = this.entity();
    else
      this.viewer().trackedEntity = undefined;
  }

  close(): void {
    this.closing.set(true);
    setTimeout(() => this.closeInfoBox.emit(), 300);
  }
}
