import { ChangeDetectionStrategy, Component, computed, input, resource } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { VoterDetailDto } from '../voter/voter.component';
import { MapPreviewComponent } from '../shared/map-preview/map-preview.component';
import { approximateAge, fullName, genderLabel, globeParams, initials } from '../shared/people/people';

@Component({
  selector: 'app-address',
  imports: [
    DatePipe,
    RouterLink,
    MapPreviewComponent,
  ],
  templateUrl: './address.component.html',
  styleUrl: './address.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AddressComponent {
  id = input.required<string>();

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

  voters = computed(() => (this.x.hasValue() ? this.x.value() : []));
  first = computed(() => this.voters().at(0));
  globe = computed(() => {
    const v = this.first();
    return v ? globeParams(v.latitude, v.longitude, v.addressId) : null;
  });

  protected readonly fullName = fullName;
  protected readonly initials = initials;
  protected readonly genderLabel = genderLabel;
  protected readonly approximateAge = approximateAge;
}
