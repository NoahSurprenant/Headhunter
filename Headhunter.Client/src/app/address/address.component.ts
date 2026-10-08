import { ChangeDetectionStrategy, Component, input, resource } from '@angular/core';
import { VoterCardComponent } from '../voter/voterCard/voterCard.component';
import { VoterDetailDto } from '../voter/voter.component';

@Component({
  selector: 'app-address',
  imports: [
    VoterCardComponent,
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

      return response.json() as Promise<VoterDetailDto[]>;
    },
  });
}
