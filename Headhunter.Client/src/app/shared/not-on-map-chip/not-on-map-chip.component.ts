import { ChangeDetectionStrategy, Component, input, output, signal } from '@angular/core';
import { ButtonComponent } from '../button/button.component';

/** The shape of ApiController.RetryGeocode's response. */
export interface RetryGeocodeResultDto {
  matched: boolean;
  latitude: number | null;
  longitude: number | null;
}

/**
 * Stands in for the map preview / "Show on globe" link on the address and voter pages when an address has no
 * Latitude/Longitude: the geocoder either failed to match it or hasn't tried yet (Address.Matched is false or
 * null). Lets the viewer retry the US Census Bureau's free geocoder for just this address, no API key needed
 * (ApiController.RetryGeocode), instead of waiting for Headhunter.Geocoding's next full batch run.
 */
@Component({
  selector: 'app-not-on-map-chip',
  imports: [
    ButtonComponent,
  ],
  template: `
    <div class="card flex flex-col items-center justify-center gap-3 text-center text-gray-500 min-h-[160px]">
      <span class="chip bg-amber-100 text-amber-800">Not on the map</span>
      <p class="text-sm">This address hasn't been placed on the map.</p>
      <x-button (clicked)="retry()" [disabled]="retrying()">{{ retrying() ? 'Retrying…' : 'Retry geocoding' }}</x-button>
      @if (message(); as m) {
        <p class="text-sm" [class.text-red-600]="!succeeded()">{{ m }}</p>
      }
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NotOnMapChipComponent {
  addressId = input.required<string>();
  /** Emitted when a retry finds coordinates, so the parent page can reload and show the map instead. */
  geocoded = output<{ latitude: number; longitude: number }>();

  retrying = signal(false);
  message = signal<string | null>(null);
  succeeded = signal(true);

  async retry(): Promise<void> {
    this.retrying.set(true);
    this.message.set(null);
    try {
      const response = await fetch(`api/address/${this.addressId()}/retrygeocode`, { method: 'POST' });
      if (response.status === 429) {
        // RetryGeocodeRateLimitOptions (ApiController/Program.cs): one shared bucket, no login to key it off.
        this.succeeded.set(false);
        this.message.set('Too many retries, try again in a minute.');
        return;
      }
      if (!response.ok) throw new Error(`RetryGeocode ${this.addressId()}: ${response.status}`);
      const result = await response.json() as RetryGeocodeResultDto;
      if (result.matched && result.latitude !== null && result.longitude !== null) {
        this.succeeded.set(true);
        this.message.set('Found it!');
        this.geocoded.emit({ latitude: result.latitude, longitude: result.longitude });
      } else {
        this.succeeded.set(false);
        this.message.set('Still not found. It may need a manual correction.');
      }
    } catch {
      this.succeeded.set(false);
      this.message.set('Retry failed. Try again later.');
    } finally {
      this.retrying.set(false);
    }
  }
}
