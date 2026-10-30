import { ChangeDetectionStrategy, Component, computed, effect, input, model } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { PaginationResult } from '../../paginationResult';
import { ButtonComponent } from '../button/button.component';
import { DropdownComponent } from '../dropdown/dropdown.component';

// 8506687 as "8,506,687". Always en-US: the site and its data are American.
const count = new Intl.NumberFormat('en-US');

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
    return `Page ${count.format(current)} of ${count.format(total)}`;
  })

  textAlt = computed(() => {
    const current = this.pageNumber();
    const size = this.pageSize();
    const total = this.total();
    const pr = this.paginationResult();

    if (pr.results.length === 0) return `0 of ${count.format(total)}`;
    const skip = (current - 1) * size;
    const start = skip + 1;
    const end = skip + pr.results.length;
    return `${count.format(start)}–${count.format(end)} of ${count.format(total)}`;
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
