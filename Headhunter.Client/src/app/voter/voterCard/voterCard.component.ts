import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { VoterDetailDto } from '../voter.component';
import { RouterModule } from '@angular/router';
import { DatePipe } from '@angular/common';

@Component({
  selector: 'app-voter-card',
  imports: [
    RouterModule,
    DatePipe,
  ],
  templateUrl: './voterCard.component.html',
  styleUrl: './voterCard.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class VoterCardComponent {
  voter = input.required<VoterDetailDto>();
  displayAddress = input<boolean>(true);
}
