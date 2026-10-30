// Synthetic, obviously fake records for unit tests. Never put real voter data here.
import type { AddressDto } from '../app/cesium/cesium.component';
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
