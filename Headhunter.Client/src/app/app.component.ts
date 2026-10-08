import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-root',
  imports: [
    RouterOutlet
  ],
  templateUrl: './app.component.html',
  // Eager, not OnPush: the routed globe page (CesiumComponent) updates plain fields from
  // raw Cesium DOM callbacks and relies on zone-triggered ticks reaching it through
  // this root. An OnPush root that is never marked dirty would skip it.
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./app.component.css'],
})
export class AppComponent {
}
