import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, effect, OnInit, resource, signal } from '@angular/core';
import { ButtonComponent } from '../shared/button/button.component';
import { PaginatorComponent } from '../shared/paginator/paginator.component';
import { PaginationResult } from '../paginationResult';
import { RouterModule } from '@angular/router';
import { FormBuilder, FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { InputComponent } from '../shared/input/input.component';
import { DropdownComponent } from '../shared/dropdown/dropdown.component';
import { Observable } from 'rxjs';
import { HttpClient } from '@angular/common/http';

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
  ],
  templateUrl: './voters.component.html',
  styleUrl: './voters.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class VotersComponent implements OnInit {

  searchFilter?: SearchFilterDto;
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

  constructor(private builder: FormBuilder, private http: HttpClient) {
    this.formGroup = builder.nonNullable.group<SearchFilterForm>({
      firstName: builder.nonNullable.control<string>(''),
      middleName: builder.nonNullable.control<string>(''),
      lastName: builder.nonNullable.control<string>(''),
      city: builder.nonNullable.control<string>(''),
      birthYear: builder.control<number | null>(null),
      age: builder.control<number | null>(null),
      astrology: builder.nonNullable.control<string>(''),
    });

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
        method: 'POST',
        headers: {
          "Content-Type": "application/json",
        },
        body: this.searchFilter ? JSON.stringify(this.searchFilter) : undefined,
      })
        .then(x => x.json() as Promise<PaginationResult<VoterDto>>);
    },
  });

  current = signal<PaginationResult<VoterDto>>({ totalCount: 0, results: []});

  onSubmit() {
    if (!this.formGroup.valid) {
      console.log('Form is invalid.');
      return;
    }

    const rawValues = this.formGroup.getRawValue();
    const searchFilter: SearchFilterDto = {
      ...rawValues,
      birthYear: rawValues.birthYear !== null ? Number(rawValues.birthYear) : null,
      age: rawValues.age !== null ? Number(rawValues.age) : null,
    };
    this.searchFilter = searchFilter;
    this.x.reload();
  }

  clearAll() {
    // Not sure why reset is not respecting nonNullable.
    //this.formGroup.reset();
    this.formGroup = this.builder.nonNullable.group<SearchFilterForm>({
      firstName: this.builder.nonNullable.control<string>(''),
      middleName: this.builder.nonNullable.control<string>(''),
      lastName: this.builder.nonNullable.control<string>(''),
      city: this.builder.nonNullable.control<string>(''),
      birthYear: this.builder.control<number | null>(null),
      age: this.builder.control<number | null>(null),
      astrology: this.builder.nonNullable.control<string>(''),
    });
    this.searchFilter = undefined;
    this.x.reload();
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
  citySuggestions = (query: string) => this.http.get<string[]>(`/api/citySuggestions?query=${query}`);

}

export interface VoterDto
{
  id: number,
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
}

export interface SearchFilterForm
{
  firstName: FormControl<string>,
  middleName: FormControl<string>,
  lastName: FormControl<string>,
  city: FormControl<string>,
  birthYear: FormControl<number | null>,
  age: FormControl<number | null>,
  astrology: FormControl<string>,
}

export interface SearchFilterDto
{
  firstName: string,
  middleName: string,
  lastName: string,
  city: string,
  birthYear: number | null,
  age: number | null,
  astrology: string,
}