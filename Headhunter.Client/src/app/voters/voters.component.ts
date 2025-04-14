import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, effect, OnInit, resource, signal } from '@angular/core';
import { ButtonComponent } from '../shared/button/button.component';
import { PaginatorComponent } from '../shared/paginator/paginator.component';
import { PaginationResult } from '../paginationResult';
import { RouterModule } from '@angular/router';

@Component({
  selector: 'app-voters',
  imports: [
    CommonModule,
    ButtonComponent,
    PaginatorComponent,
    RouterModule,
  ],
  templateUrl: './voters.component.html',
  styleUrl: './voters.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class VotersComponent implements OnInit {

  constructor() {
    effect(() => {
      const x = this.x.value();
      if (x)
        this.current.set(x);
    });
  }

  isEven(i: number) {
    return i % 2 == 0;
  }

  ngOnInit(): void {
    this.x.reload();
  }

  clicked(): void {
    this.x.reload();
  }

  pageSize = signal(10);
  pageNumber = signal(1);

  x = resource({
    request: () => ({ pageSize: this.pageSize(),
                      pageNumber: this.pageNumber(),
                    }),
    loader: async ({request}) => {
      const params = new URLSearchParams();
      params.set('pageSize', request.pageSize.toString());
      params.set('pageNumber', request.pageNumber.toString());
      return await fetch(`Api/voters?${params}`, {
        method: 'GET',
        headers: {
          "Content-Type": "application/json",
        },
      })
        .then(x => x.json() as Promise<PaginationResult<VoterDto>>);
    },
  });

  current = signal<PaginationResult<VoterDto>>({ totalCount: 0, results: []});

}

export interface VoterDto
{
  id: number,
  firstName: string,
  lastName: string,
}