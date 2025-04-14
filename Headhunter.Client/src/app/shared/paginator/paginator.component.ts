import { ChangeDetectionStrategy, Component, computed, effect, input, model } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { PaginationResult } from '../../paginationResult';
import { ButtonComponent } from '../button/button.component';
import { DropdownComponent } from '../dropdown/dropdown.component';

@Component({
  selector: 'x-paginator',
  imports: [
    DropdownComponent,
    ReactiveFormsModule,
    ButtonComponent,
],
  templateUrl: './paginator.component.html',
  styleUrl: './paginator.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PaginatorComponent {
  pageSize = model<number>(10);
  pageNumber = model<number>(1);

  paginationResult = input.required<PaginationResult<any>>();
  total = computed(() => {
    const pr = this.paginationResult();
    return pr.totalCount;
  });

  formControl = new FormControl<number>(0);

  pageTotal = computed(() => {
    const current = this.pageSize();
    const total = this.total();
    return Math.ceil(total / current);
  });

  text = computed(() => {
    const current = this.pageNumber();
    const total = this.pageTotal();
    return `Page ${current} of ${total}`;
  })

  textAlt = computed(() => {
    const current = this.pageNumber();
    const size = this.pageSize();
    const total = this.total();
    const pr = this.paginationResult();

    const skip = (current - 1) * size;
    const start = skip + 1;
    const end = skip + pr.results.length;
    return `Items ${start} - ${end} of ${total}`;
  })

  hasPreviousPage = computed(() => {
    const page = this.pageNumber();
    return page > 1;
  })

  hasNextPage = computed(() => {
    const page = this.pageNumber();
    const total = this.pageTotal();
    return total > page;
  })

  constructor() {
    this.formControl.valueChanges.subscribe({
      next: (x) => this.pageSize.set(x ?? 1),
    });

    effect(() => {
      this.formControl.patchValue(this.pageSize(), { emitEvent: false });
    });
  }

  previous(): void {
    this.pageNumber.set(this.pageNumber() - 1);
  }

  next(): void {
    this.pageNumber.set(this.pageNumber() + 1);
  }
}
