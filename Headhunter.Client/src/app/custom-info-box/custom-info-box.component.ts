import { animate, state, style, transition, trigger } from '@angular/animations';
import { AfterContentInit, Component, HostBinding, input, output } from '@angular/core';
import { Entity, Viewer } from 'cesium';

@Component({
  selector: 'app-custom-info-box',
  templateUrl: './custom-info-box.component.html',
  styleUrls: ['./custom-info-box.component.css'],
  animations: [
    trigger('slideInOut', [
      state('in', style({ transform: 'translateX(0)', opacity: 1 })),
      state('out', style({ transform: 'translateX(100%)', opacity: 0 })),
      transition('out => in', [
        style({ transform: 'translateX(100%)', opacity: 0 }),
        animate('300ms ease-out')
      ]),
      transition('in => out', [
        animate('300ms ease-in', style({ transform: 'translateX(100%)', opacity: 0 }))
      ])
    ])
  ],
})
export class CustomInfoBoxComponent implements AfterContentInit {
  ngAfterContentInit(): void {
    setTimeout(() => {
      this.animationState = 'in';
    }, 0);
  }

  viewer = input.required<Viewer>();
  entity = input.required<Entity>();
  closeInfoBox = output<void>();
  @HostBinding('@slideInOut') animationState = 'out';
  
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
    this.animationState = 'out';
    setTimeout(() => this.closeInfoBox.emit(), 300);
  }
}
