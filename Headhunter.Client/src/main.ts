import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { AppComponent } from './app/app.component';

window['CESIUM_BASE_URL'] = '/assets/cesium/';

bootstrapApplication(AppComponent, appConfig)
  .catch((err) => console.error(err));

declare global {
  interface Window {
    CESIUM_BASE_URL: string;
  }
}
