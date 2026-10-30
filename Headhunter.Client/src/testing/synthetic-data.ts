// Synthetic, obviously fake records for unit tests. Never put real voter data here.
import type { AddressDto } from '../app/cesium/cesium.component';
import type { DistrictStatsDto } from '../app/districts/districts';
import type { PaginationResult } from '../app/paginationResult';
import type { RelativeDto, VoterDetailDto } from '../app/voter/voter.component';
import type { VoterDto } from '../app/voters/voters.component';

export function voterDetail(overrides: Partial<VoterDetailDto> = {}): VoterDetailDto {
  return {
    id: 'voter-0001',
    firstName: 'Alex',
    middleName: 'Quinn',
    lastName: 'Example',
    birthYear: 1990,
    gender: 'F',
    registrationDate: '2020-03-15T00:00:00', // the API sends ISO strings
    addressId: 'addr-0001',
    fullStreetAddress: '123 Fake St',
    city: 'Testville',
    state: 'MI',
    zip: '49999',
    latitude: 43.0,
    longitude: -84.5,
    hasMailingAddress: false,
    mailingAddressOutOfState: false,
    mailingAddressLines: [],
    ...overrides,
  };
}

export function voterRow(overrides: Partial<VoterDto> = {}): VoterDto {
  return {
    id: 'voter-1001',
    addressId: 'addr-0001',
    firstName: 'Alex',
    middleName: '',
    lastName: 'Example',
    birthYear: 1990,
    gender: 'X',
    streetNumberPrefix: '',
    streetNumber: '123',
    streetNumberSuffix: '',
    directionPrefix: '',
    streetName: 'Fake',
    streetType: 'St',
    directionSuffix: '',
    extension: '',
    city: 'Testville',
    state: 'MI',
    zipCode: '49999',
    latitude: null,
    longitude: null,
    hasMailingAddress: false,
    mailingAddressOutOfState: false,
    ...overrides,
  };
}

export function votersPage(results: VoterDto[], totalCount = results.length): PaginationResult<VoterDto> {
  return { totalCount, results };
}

export function relative(overrides: Partial<RelativeDto> = {}): RelativeDto {
  return {
    id: 'voter-0003',
    firstName: 'Robin',
    middleName: '',
    lastName: 'Example',
    birthYear: 1960,
    gender: 'M',
    addressId: 'addr-0002',
    fullStreetAddress: '456 Nearby Rd',
    city: 'Testville',
    state: 'MI',
    zip: '49999',
    latitude: 43.01,
    longitude: -84.51,
    distanceKm: 1.3,
    ...overrides,
  };
}

export function areaAddress(overrides: Partial<AddressDto> = {}): AddressDto {
  return {
    id: 'addr-0001',
    streetNumber: '123',
    streetName: 'Fake St',
    city: 'Testville',
    state: 'MI',
    zipCode: '49999',
    latitude: 43.0,
    longitude: -84.5,
    voters: [
      { id: 'voter-0001', firstName: 'Alex', lastName: 'Example' },
      { id: 'voter-0002', firstName: 'Jordan', lastName: 'Sample' },
    ],
    ...overrides,
  };
}

export function districtStats(overrides: Partial<DistrictStatsDto> = {}): DistrictStatsDto {
  return {
    type: 'Precinct',
    name: '00012',
    within: 'CITY OF TESTVILLE',
    voters: 1240,
    households: 610,
    ages: [
      { label: 'Under 25', count: 120 },
      { label: '25–34', count: 240 },
      { label: '35–44', count: 200 },
      { label: '45–54', count: 180 },
      { label: '55–64', count: 220 },
      { label: '65–74', count: 160 },
      { label: '75+', count: 120 },
    ],
    genders: [{ gender: 'F', count: 651 }, { gender: 'M', count: 589 }],
    permanentAbsenteePercent: 37.5,
    newestRegistrations: [
      { id: 'voter-2001', firstName: 'Alex', middleName: '', lastName: 'Example', registrationDate: '2026-09-30T00:00:00' },
      { id: 'voter-2002', firstName: 'Jordan', middleName: 'Q', lastName: 'Sample', registrationDate: '2026-08-12T00:00:00' },
    ],
    extent: { west: -84.6, east: -84.5, south: 42.7, north: 42.8 },
    ...overrides,
  };
}
