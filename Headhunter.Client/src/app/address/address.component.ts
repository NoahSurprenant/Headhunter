import { ChangeDetectionStrategy, Component, computed, inject, input, resource, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { VoterDetailDto } from '../voter/voter.component';
import { MapPreviewComponent } from '../shared/map-preview/map-preview.component';
import { RegistrationTimelineComponent } from '../shared/registration-timeline/registration-timeline.component';
import { approximateAge, formatDistance, fullName, genderLabel, globeParams, initials, registrationDateOrNull } from '../shared/people/people';

/** One neighbouring house: enough to link to it. */
export interface WalkNeighborDto {
  id: string;
  fullStreetAddress: string;
}

/** One nearby household, ranked by straight-line distance from the current address. */
export interface NearbyAddressDto {
  id: string;
  fullStreetAddress: string;
  voterCount: number;
  distanceMeters: number;
}

/** "Walk the street": the previous/next house on the block, plus the closest households by distance. */
export interface WalkDto {
  previous: WalkNeighborDto | null;
  next: WalkNeighborDto | null;
  nearby: NearbyAddressDto[];
}

@Component({
  selector: 'app-address',
  imports: [
    DatePipe,
    RouterLink,
    MapPreviewComponent,
    RegistrationTimelineComponent,
  ],
  templateUrl: './address.component.html',
  styleUrl: './address.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
  // ArrowLeft/ArrowRight walk to the previous/next house, same as the Previous/Next buttons.
  host: {
    '(document:keydown)': 'onKeydown($event)',
  },
})
export class AddressComponent {
  private router = inject(Router);

  id = input.required<string>();

  // Same side of the street (odd/even house numbers) by default; toggled to see both sides.
  sameSideOnly = signal(true);

  x = resource({
    params: () => ({
      id: this.id(),
    }),
    loader: async ({ params, abortSignal }) => {
      const response = await fetch(`api/address/${params.id}`, {
        method: 'GET',
        signal: abortSignal,
      });
      if (!response.ok) throw new Error(`Address ${params.id}: ${response.status}`);
      return response.json() as Promise<VoterDetailDto[]>;
    },
  });

  walk = resource({
    params: () => ({
      id: this.id(),
      sameSideOnly: this.sameSideOnly(),
    }),
    loader: async ({ params, abortSignal }) => {
      const response = await fetch(`api/address/${params.id}/walk?sameSideOnly=${params.sameSideOnly}`, {
        method: 'GET',
        signal: abortSignal,
      });
      if (!response.ok) throw new Error(`Walk ${params.id}: ${response.status}`);
      return response.json() as Promise<WalkDto>;
    },
  });

  voters = computed(() => (this.x.hasValue() ? this.x.value() : []));
  first = computed(() => this.voters().at(0));
  globe = computed(() => {
    const v = this.first();
    return v ? globeParams(v.latitude, v.longitude, v.addressId) : null;
  });

  previousHouse = computed(() => (this.walk.hasValue() ? this.walk.value().previous ?? null : null));
  nextHouse = computed(() => (this.walk.hasValue() ? this.walk.value().next ?? null : null));
  nearby = computed(() => (this.walk.hasValue() ? this.walk.value().nearby ?? [] : []));

  toggleSameSide(): void {
    this.sameSideOnly.set(!this.sameSideOnly());
  }

  onKeydown(event: KeyboardEvent): void {
    // Don't hijack arrow keys while the user is typing somewhere (not that this page has inputs, but
    // other pages linking here might render it inside one).
    const target = event.target as HTMLElement | null;
    if (target && (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA')) return;

    if (event.key === 'ArrowLeft' && this.previousHouse()) {
      this.router.navigate(['/address', this.previousHouse()!.id]);
    } else if (event.key === 'ArrowRight' && this.nextHouse()) {
      this.router.navigate(['/address', this.nextHouse()!.id]);
    }
  }

  protected readonly fullName = fullName;
  protected readonly initials = initials;
  protected readonly genderLabel = genderLabel;
  protected readonly approximateAge = approximateAge;
  protected readonly formatDistance = formatDistance;
  protected readonly registrationDateOrNull = registrationDateOrNull;
}
