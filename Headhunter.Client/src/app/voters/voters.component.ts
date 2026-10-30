import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, OnInit, resource, signal } from '@angular/core';
import { ButtonComponent } from '../shared/button/button.component';
import { PaginatorComponent } from '../shared/paginator/paginator.component';
import { PaginationResult } from '../paginationResult';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { FormBuilder, FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { InputComponent } from '../shared/input/input.component';
import { DropdownComponent } from '../shared/dropdown/dropdown.component';
import { Observable } from 'rxjs';
import { HttpClient } from '@angular/common/http';
import { CesiumComponent } from '../cesium/cesium.component';
import { AreaBox, boxFromParams, boxParams } from '../cesium/area-selection';
import { approximateAge, fullName, genderLabel } from '../shared/people/people';
import { DistrictCardComponent } from '../districts/district-card/district-card.component';
import { DISTRICT_TYPES, districtRef, districtTypeOption } from '../districts/districts';
import {
  appendConditionParams, Choice, CONDITION_FIELDS, ConditionRow, decodeConditions, encodeConditions, fieldOf, MAX_CONDITIONS,
  newRow, operatorsFor, SearchConditionDto, SearchProblem, SLOW_OPERATORS, toDto, TOO_BROAD_MESSAGE, withField,
} from './advanced-search';

@Component({
  selector: 'app-voters',
  imports: [
    CommonModule,
    ButtonComponent,
    PaginatorComponent,
    RouterModule,
    ReactiveFormsModule,
    InputComponent,
    DropdownComponent,
    CesiumComponent,
    DistrictCardComponent,
  ],
  templateUrl: './voters.component.html',
  styleUrl: './voters.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class VotersComponent implements OnInit {

  searchFilter = signal<SearchFilterDto | undefined>(undefined);
  formGroup: FormGroup<SearchFilterForm>;

  astrologyOptions: string[] = [
    '',
    'Aries',
    'Taurus',
    'Gemini',
    'Cancer',
    'Leo',
    'Virgo',
    'Libra',
    'Scorpio',
    'Sagittarius',
    'Capricorn',
    'Aquarius',
    'Pisces',
  ];

  readonly districtTypes = DISTRICT_TYPES;

  constructor(private builder: FormBuilder, private http: HttpClient) {
    this.formGroup = this.newForm();

    effect(() => {
      // value() throws while the resource is in an error state (Angular 20+), so guard with hasValue().
      if (this.x.hasValue())
        this.current.set(this.x.value());
      // A search the API turned down: its rows would be the last search's, not this one's.
      else if (this.x.error() instanceof SearchProblem)
        this.current.set({ totalCount: 0, results: [] });
    });
  }

  // The row picked in the list: the globe beside it flies to that voter's address.
  selected = signal<VoterDto | undefined>(undefined);

  select(voter: VoterDto): void {
    this.selected.set(voter);
  }

  protected readonly approximateAge = approximateAge;
  protected readonly fullName = fullName;
  protected readonly genderLabel = genderLabel;

  isEven(i: number) {
    return i % 2 == 0;
  }

  ngOnInit(): void {
    this.restoreFromUrl();
    this.x.reload();
  }

  clicked(): void {
    this.x.reload();
  }

  pageSize = signal(10);
  pageNumber = signal(1);

  private router = inject(Router);
  private route = inject(ActivatedRoute);

  // The box drawn on the globe (Select area): only voters living inside it. Kept in ?west=&east=&north=&south=
  // so a reload or a shared link keeps it.
  area = signal<AreaBox | undefined>(boxFromParams(name => this.route.snapshot.queryParamMap.get(name)));

  selectArea(box: AreaBox | undefined): void {
    this.area.set(box);
    this.pageNumber.set(1);
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: boxParams(box),
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }

  clearArea(): void {
    this.selectArea(undefined);
  }

  // What the list shows, as the API's SearchFilterDto: the form's search and the area. Undefined for everyone.
  private filter = computed(() => {
    const searchFilter = this.searchFilter();
    const area = this.area();
    return searchFilter || area ? { ...searchFilter, ...area } : undefined;
  });

  // The whole result as CSV (every page, up to the API's Export:MaxRows), for a plain link: the browser streams
  // the download to disk itself.
  readonly csvLimit = 50_000;
  csvUrl = computed(() => {
    const query = new URLSearchParams();
    const filter = this.filter();
    Object.entries(filter ?? {})
      .filter(([key, value]) => key !== 'conditions' && value !== null && value !== undefined && value !== '')
      .forEach(([key, value]) => query.set(key, String(value)));
    // As conditions[0].field=..&conditions[0].operator=.., which the API's [FromQuery] binds to the list.
    appendConditionParams(query, filter?.conditions ?? []);
    if (this.mailingAddressDiffers())
      query.set('mailingAddressDiffers', 'true');
    const q = query.toString();
    return q ? `Api/VotersCsv?${q}` : 'Api/VotersCsv';
  });

  // A query param rather than part of SearchFilterDto's body: it's its own toggle, combinable with whatever's
  // already in the search form (first name, city, ...), not a replacement for it.
  mailingAddressDiffers = signal(false);

  onMailingAddressDiffersChange(event: Event): void {
    this.mailingAddressDiffers.set((event.target as HTMLInputElement).checked);
    this.x.reload();
  }

  x = resource({
    params: () => ({
      pageSize: this.pageSize(),
      pageNumber: this.pageNumber(),
      filter: this.filter(),
      mailingAddressDiffers: this.mailingAddressDiffers(),
    }),
    loader: async ({ params, abortSignal }) => {
      const query = new URLSearchParams();
      query.set('pageSize', params.pageSize.toString());
      query.set('pageNumber', params.pageNumber.toString());
      if (params.mailingAddressDiffers)
        query.set('mailingAddressDiffers', 'true');

      const response = await fetch(`Api/voters?${query}`, {
        method: 'POST',
        headers: {
          "Content-Type": "application/json",
        },
        body: params.filter ? JSON.stringify(params.filter) : undefined,
        signal: abortSignal,
      });

      // Ran out of the API's Search:CommandTimeout, or a condition it can't use.
      if (response.status === 422)
        throw new SearchProblem(TOO_BROAD_MESSAGE);
      if (response.status === 400) {
        const problem = await response.json().catch(() => undefined) as { detail?: string } | undefined;
        throw new SearchProblem(problem?.detail ?? 'The API could not use this search.');
      }
      return response.json() as Promise<PaginationResult<VoterDto>>;
    },
  });

  current = signal<PaginationResult<VoterDto>>({ totalCount: 0, results: []});

  // Why the last search has no results: too broad to finish, or a bad condition.
  searchProblem = computed(() => {
    const error = this.x.error();
    return error instanceof SearchProblem ? error.message : undefined;
  });

  // The advanced search: rows of field, operator and value, ANDed with the form above. Sent with Search, like
  // the form, and kept in ?cond= (advanced-search.ts encodes it).
  advanced = signal(false);
  conditions = signal<ConditionRow[]>([]);
  voterStatuses = signal<string[]>([]);
  private voterStatusesRequested = false;

  protected readonly conditionFields = CONDITION_FIELDS;
  protected readonly maxConditions = MAX_CONDITIONS;
  protected readonly fieldOf = fieldOf;
  protected readonly operatorsFor = operatorsFor;

  isSlow(row: ConditionRow): boolean {
    return SLOW_OPERATORS.has(row.op);
  }

  /** A choice field's options; the voter statuses are whatever the voter file has (plus one from a link). */
  choicesFor(row: ConditionRow): Choice[] {
    if (row.field !== 'voterStatus')
      return fieldOf(row.field)?.choices ?? [];
    const codes = this.voterStatuses();
    return [...codes, ...(row.value && !codes.includes(row.value) ? [row.value] : [])].map(c => ({ value: c, label: c }));
  }

  toggleAdvanced(): void {
    const open = !this.advanced();
    this.advanced.set(open);
    if (open) {
      this.loadVoterStatuses();
      if (this.conditions().length === 0)
        this.addCondition();
    }
  }

  addCondition(): void {
    if (this.conditions().length >= MAX_CONDITIONS) return;
    this.conditions.update(rows => [...rows, newRow()]);
  }

  removeCondition(id: number): void {
    this.conditions.update(rows => rows.filter(r => r.id !== id));
  }

  setConditionField(id: number, field: string): void {
    this.updateCondition(id, row => withField(row, field, this.choicesFor({ ...row, field, value: '' })));
  }

  setConditionOperator(id: number, op: string): void {
    this.updateCondition(id, row => ({ ...row, op }));
  }

  setConditionValue(id: number, which: 'value' | 'value2', value: string): void {
    this.updateCondition(id, row => ({ ...row, [which]: value }));
  }

  private updateCondition(id: number, change: (row: ConditionRow) => ConditionRow): void {
    this.conditions.update(rows => rows.map(r => (r.id === id ? change(r) : r)));
  }

  private loadVoterStatuses(): void {
    if (this.voterStatusesRequested) return;
    this.voterStatusesRequested = true;
    this.http.get<string[]>('/api/voterStatuses').subscribe({
      next: codes => {
        this.voterStatuses.set(codes);
        // A voter status row added before the list came has no value yet: the first code.
        this.conditions.update(rows => rows.map(r => (r.field === 'voterStatus' && !r.value ? { ...r, value: codes[0] ?? '' } : r)));
      },
      error: () => (this.voterStatusesRequested = false),
    });
  }

  onSubmit() {
    if (!this.formGroup.valid) {
      console.log('Form is invalid.');
      return;
    }

    const searchFilter = this.filterFromForm();
    this.searchFilter.set(searchFilter);
    this.keepInUrl(searchFilter);
    this.x.reload();
  }

  private filterFromForm(): SearchFilterDto {
    const rawValues = this.formGroup.getRawValue();
    const conditions = toDto(this.conditions());
    return {
      // Only rows filled in; none leaves the body as it was before the advanced search.
      ...(conditions.length ? { conditions } : {}),
      ...rawValues,
      birthYear: rawValues.birthYear !== null ? Number(rawValues.birthYear) : null,
      age: rawValues.age !== null ? Number(rawValues.age) : null,
      // The API reads the type by name; none is null, not "".
      districtType: rawValues.districtType || null,
      // Only precincts, wards and commissioner districts are within something.
      districtWithin: districtTypeOption(rawValues.districtType)?.within ? rawValues.districtWithin : '',
    };
  }

  clearAll() {
    // Not sure why reset is not respecting nonNullable.
    //this.formGroup.reset();
    this.formGroup = this.newForm();
    this.searchFilter.set(undefined);
    this.mailingAddressDiffers.set(false);
    this.conditions.set([]);
    this.advanced.set(false);
    // The area too, in the same navigation: two in a row would each merge into the URL as it was.
    this.area.set(undefined);
    this.pageNumber.set(1);
    this.keepInUrl(undefined, boxParams(undefined));
    this.x.reload();
  }

  private newForm(): FormGroup<SearchFilterForm> {
    return this.builder.nonNullable.group<SearchFilterForm>({
      firstName: this.builder.nonNullable.control<string>(''),
      middleName: this.builder.nonNullable.control<string>(''),
      lastName: this.builder.nonNullable.control<string>(''),
      street: this.builder.nonNullable.control<string>(''),
      city: this.builder.nonNullable.control<string>(''),
      birthYear: this.builder.control<number | null>(null),
      age: this.builder.control<number | null>(null),
      astrology: this.builder.nonNullable.control<string>(''),
      districtType: this.builder.nonNullable.control<string>(''),
      district: this.builder.nonNullable.control<string>(''),
      districtWithin: this.builder.nonNullable.control<string>(''),
    });
  }

  addressColumn(voter: VoterDto): string {
    const arr: string[] = [];
    if (voter.streetNumberPrefix)
      arr.push(voter.streetNumberPrefix);
    if (voter.streetNumber)
      arr.push(voter.streetNumber);
    if (voter.streetNumberSuffix)
      arr.push(voter.streetNumberSuffix);
    if (voter.directionPrefix)
      arr.push(voter.directionPrefix);
    if (voter.streetName)
      arr.push(voter.streetName);
    if (voter.streetType)
      arr.push(voter.streetType);
    if (voter.directionSuffix)
      arr.push(voter.directionSuffix);
    if (voter.extension)
      arr.push(voter.extension);
    return arr.join(' ');
  }

  firstNameSuggestions = (query: string) => this.http.get<string[]>(`/api/firstNameSuggestions?query=${query}`);
  middleNameSuggestions = (query: string) => this.http.get<string[]>(`/api/middleNameSuggestions?query=${query}`);
  lastNameSuggestions = (query: string) => this.http.get<string[]>(`/api/lastNameSuggestions?query=${query}`);
  streetSuggestions = (query: string) => this.http.get<string[]>(`/api/streetSuggestions?query=${query}`);
  citySuggestions = (query: string) => this.http.get<string[]>(`/api/citySuggestions?query=${query}`);
  // The district's names, of the type picked (and in the jurisdiction or county, for precincts, wards and commissioner
  // districts), and the jurisdictions or counties those are in.
  districtSuggestions = (query: string) => {
    const { districtType, districtWithin } = this.formGroup.getRawValue();
    return this.http.get<string[]>('/api/districtSuggestions', { params: { type: districtType, query, within: districtWithin } });
  };
  districtWithinSuggestions = (query: string) => {
    const within = districtTypeOption(this.formGroup.getRawValue().districtType)?.within ?? 'Jurisdiction';
    return this.http.get<string[]>('/api/districtSuggestions', { params: { type: within, query } });
  };

  /** What the district needs to be in (Jurisdiction or County), if anything. */
  districtWithin(): string | undefined {
    return districtTypeOption(this.formGroup.controls.districtType.value)?.within;
  }

  // Another type's names mean nothing: picking a type starts the district over.
  districtTypeChanged(): void {
    this.formGroup.patchValue({ district: '', districtWithin: '' });
  }

  // The district searched for: its card beside the results, and the globe flies to it and shows only its addresses.
  activeDistrict = computed(() => {
    const f = this.searchFilter();
    return f ? districtRef(f.districtType, f.district, f.districtWithin) : undefined;
  });

  // The search lives in the URL (?lastName=..&districtType=..), so it can be shared, bookmarked and come back to.
  // Merged, so the globe's own params (the selected area, the camera view) stay; empty fields are removed.
  private keepInUrl(filter: SearchFilterDto | undefined, extra: Record<string, number | null> = {}): void {
    const queryParams: Record<string, string | number | null> = { ...extra };
    for (const key of Object.keys(this.formGroup.controls)) {
      const value = (filter as Record<string, unknown> | undefined)?.[key];
      queryParams[key] = value !== null && value !== undefined && value !== '' ? String(value) : null;
    }
    // Every row, even one still without a value, so the page comes back as it was left.
    queryParams['cond'] = filter && this.conditions().length ? encodeConditions(this.conditions()) : null;
    this.router.navigate([], { relativeTo: this.route, queryParams, queryParamsHandling: 'merge' });
  }

  private restoreFromUrl(): void {
    const params = this.route.snapshot.queryParamMap;
    const keys = Object.keys(this.formGroup.controls).filter(k => params.has(k));
    const conditions = decodeConditions(params.get('cond'));
    if (conditions.length) {
      this.conditions.set(conditions);
      this.advanced.set(true);
      this.loadVoterStatuses();
    }
    if (keys.length === 0 && conditions.length === 0) return;
    for (const key of keys) {
      this.formGroup.get(key)!.setValue(params.get(key));
    }
    this.searchFilter.set(this.filterFromForm());
  }

}

export interface VoterDto
{
  id: string,
  addressId: string,
  firstName: string,
  middleName: string,
  lastName: string,
  birthYear: number,
  gender: string,
  streetNumberPrefix: string,
  streetNumber: string,
  streetNumberSuffix: string,
  directionPrefix: string,
  streetName: string,
  streetType: string,
  directionSuffix: string,
  extension: string,
  city: string,
  state: string,
  zipCode: string,
  latitude: number | null,
  longitude: number | null,
  /** Mail doesn't go to this voter's residence. */
  hasMailingAddress: boolean,
  /** Mail goes somewhere outside `state`, best-effort from the mailing address's last line. */
  mailingAddressOutOfState: boolean,
}

export interface SearchFilterForm
{
  firstName: FormControl<string>,
  middleName: FormControl<string>,
  lastName: FormControl<string>,
  street: FormControl<string>,
  city: FormControl<string>,
  birthYear: FormControl<number | null>,
  age: FormControl<number | null>,
  astrology: FormControl<string>,
  districtType: FormControl<string>,
  district: FormControl<string>,
  districtWithin: FormControl<string>,
}

export interface SearchFilterDto
{
  firstName: string,
  middleName: string,
  lastName: string,
  street: string,
  city: string,
  birthYear: number | null,
  age: number | null,
  astrology: string,
  districtType: string | null,
  district: string,
  districtWithin: string,
  /** The advanced search's rows that are filled in; left out when there are none. */
  conditions?: SearchConditionDto[],
}
