import { ChangeDetectionStrategy, Component, input, resource } from '@angular/core';
import { VoterCardComponent } from './voterCard/voterCard.component';

@Component({
  selector: 'app-voter',
  imports: [
    VoterCardComponent,
  ],
  templateUrl: './voter.component.html',
  styleUrl: './voter.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class VoterComponent {
  id = input.required<string>();
  displayAddress = input<boolean>(true);

  x = resource({
    request: () => ({
      id: this.id(),
    }),
    loader: async ({ request, abortSignal }) => {
      const response = await fetch(`api/voter/${request.id}`, {
        method: 'GET',
        signal: abortSignal,
      });

      return response.json() as Promise<VoterDetailDto>;
    },
  });
}

export interface VoterDetailDto {
  firstName: string,
  middleName: string,
  lastName: string,
  birthYear: number,
  registrationDate: Date,
  addressId: string,
  fullStreetAddress: string,
  city: string;
  state: string,
  zip: string,
};