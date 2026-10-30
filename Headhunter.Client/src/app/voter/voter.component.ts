import { ChangeDetectionStrategy, Component, computed, input, resource } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MapPreviewComponent } from '../shared/map-preview/map-preview.component';
import { NotOnMapChipComponent } from '../shared/not-on-map-chip/not-on-map-chip.component';
import { approximateAge, fullName, genderLabel, globeParams, initials, yearsAgoLabel } from '../shared/people/people';

@Component({
  selector: 'app-voter',
  imports: [
    DatePipe,
    DecimalPipe,
    RouterLink,
    MapPreviewComponent,
    NotOnMapChipComponent,
  ],
  templateUrl: './voter.component.html',
  styleUrl: './voter.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class VoterComponent {
  id = input.required<string>();

  x = resource({
    params: () => ({
      id: this.id(),
    }),
    loader: async ({ params, abortSignal }) => {
      const response = await fetch(`api/voter/${params.id}`, {
        method: 'GET',
        signal: abortSignal,
      });
      if (!response.ok) throw new Error(`Voter ${params.id}: ${response.status}`);
      return response.json() as Promise<VoterDetailDto>;
    },
  });

  // Everyone else registered at the same address.
  household = resource({
    params: () => (this.x.hasValue() ? { addressId: this.x.value().addressId } : undefined),
    loader: async ({ params, abortSignal }) => {
      const response = await fetch(`api/address/${params.addressId}`, { method: 'GET', signal: abortSignal });
      if (!response.ok) return [] as VoterDetailDto[];
      return response.json() as Promise<VoterDetailDto[]>;
    },
  });
  others = computed(() => {
    const all = this.household.hasValue() ? this.household.value() : [];
    return (Array.isArray(all) ? all : []).filter(v => v.id !== this.id());
  });

  // Other voters with the same last name nearby or on the same street, outside this household.
  relatives = resource({
    params: () => ({ id: this.id() }),
    loader: async ({ params, abortSignal }) => {
      const response = await fetch(`api/voter/${params.id}/relatives`, { method: 'GET', signal: abortSignal });
      if (!response.ok) return [] as RelativeDto[];
      return response.json() as Promise<RelativeDto[]>;
    },
  });
  relativesList = computed(() => {
    const all = this.relatives.hasValue() ? this.relatives.value() : [];
    return Array.isArray(all) ? all : [];
  });

  globe = computed(() => {
    if (!this.x.hasValue()) return null;
    const v = this.x.value();
    return globeParams(v.latitude, v.longitude, v.addressId);
  });

  protected readonly fullName = fullName;
  protected readonly initials = initials;
  protected readonly genderLabel = genderLabel;
  protected readonly approximateAge = approximateAge;
  protected readonly globeParams = globeParams;
  protected readonly yearsAgoLabel = yearsAgoLabel;
}

export interface VoterDetailDto {
  id: string,
  firstName: string,
  middleName: string,
  lastName: string,
  birthYear: number,
  gender: string,
  registrationDate: string,
  addressId: string,
  fullStreetAddress: string,
  city: string;
  state: string,
  zip: string,
  latitude: number | null,
  longitude: number | null,
  /** Mail doesn't go to this voter's residence. */
  hasMailingAddress: boolean,
  /** Mail goes somewhere outside `state`, best-effort from the mailing address's last line. */
  mailingAddressOutOfState: boolean,
  /** The non-empty mailing address lines, in order. Empty when hasMailingAddress is false. */
  mailingAddressLines: string[],
};

/** A possible relative: same last name, nearby or on the same street, outside the voter's household. */
export interface RelativeDto {
  id: string,
  firstName: string,
  middleName: string,
  lastName: string,
  birthYear: number,
  gender: string,
  addressId: string,
  fullStreetAddress: string,
  city: string,
  state: string,
  zip: string,
  latitude: number | null,
  longitude: number | null,
  distanceKm: number | null,
};
