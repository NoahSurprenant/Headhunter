import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, effect, resource, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { debounceTime } from 'rxjs';
import { PaginationResult } from '../paginationResult';
import { ButtonComponent } from '../shared/button/button.component';
import { InputComponent } from '../shared/input/input.component';
import { PaginatorComponent } from '../shared/paginator/paginator.component';

/** ApiController.NotOnMapCounties: how many unmatched addresses are in each county. */
export interface CountyCountDto {
  county: string;
  count: number;
}

/** One row of ApiController.NotOnMap's paged list. */
export interface NotOnMapAddressDto {
  id: string;
  fullStreetAddress: string;
  city: string;
  state: string;
  zipCode: string;
  county: string;
  matched: boolean | null;
}

/**
 * Addresses the geocoder hasn't placed: Matched is false (tried, no match) or still null (a batch run hasn't
 * reached them yet). Filterable by city or county, with a per-county breakdown up top so the gaps are visible
 * before picking a filter; each row links to its address page.
 */
@Component({
  selector: 'app-not-on-map',
  imports: [
    RouterLink,
    ReactiveFormsModule,
    ButtonComponent,
    InputComponent,
    PaginatorComponent,
  ],
  templateUrl: './not-on-map.component.html',
  styleUrl: './not-on-map.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NotOnMapComponent {
  cityControl = new FormControl('', { nonNullable: true });
  cityFilter = signal('');
  countyFilter = signal('');
  pageNumber = signal(1);
  pageSize = signal(10);

  constructor(private http: HttpClient) {
    this.cityControl.valueChanges.pipe(debounceTime(300)).subscribe(value => {
      this.cityFilter.set(value);
      this.pageNumber.set(1);
    });

    // value() throws while the resource is in an error state, so guard with hasValue() (see VotersComponent).
    effect(() => {
      if (this.list.hasValue())
        this.current.set(this.list.value());
    });
  }

  counties = resource({
    loader: async ({ abortSignal }) => {
      const response = await fetch('api/notonmap/counties', { signal: abortSignal });
      if (!response.ok) return [] as CountyCountDto[];
      return response.json() as Promise<CountyCountDto[]>;
    },
  });

  countiesList = computed(() => (this.counties.hasValue() ? this.counties.value() : []));
  totalMissing = computed(() => this.countiesList().reduce((sum, c) => sum + c.count, 0));

  list = resource({
    params: () => ({
      city: this.cityFilter(),
      county: this.countyFilter(),
      pageNumber: this.pageNumber(),
      pageSize: this.pageSize(),
    }),
    loader: async ({ params, abortSignal }) => {
      const query = new URLSearchParams();
      query.set('pageNumber', params.pageNumber.toString());
      query.set('pageSize', params.pageSize.toString());
      if (params.city) query.set('city', params.city);
      if (params.county) query.set('county', params.county);

      const response = await fetch(`api/notonmap?${query}`, { signal: abortSignal });
      if (!response.ok) throw new Error(`NotOnMap: ${response.status}`);
      return response.json() as Promise<PaginationResult<NotOnMapAddressDto>>;
    },
  });

  current = signal<PaginationResult<NotOnMapAddressDto>>({ totalCount: 0, results: [] });

  selectCounty(county: string): void {
    this.countyFilter.set(this.countyFilter() === county ? '' : county);
    this.pageNumber.set(1);
  }

  clearFilters(): void {
    this.cityControl.setValue('');
    this.cityFilter.set('');
    this.countyFilter.set('');
    this.pageNumber.set(1);
  }

  hasFilters(): boolean {
    return this.cityFilter() !== '' || this.countyFilter() !== '';
  }

  statusLabel(matched: boolean | null): string {
    return matched === false ? 'Failed to match' : 'Not yet attempted';
  }

  citySuggestions = (query: string) => this.http.get<string[]>(`/api/citySuggestions?query=${query}`);
}
