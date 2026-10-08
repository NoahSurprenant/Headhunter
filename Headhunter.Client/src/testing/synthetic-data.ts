// Synthetic, obviously fake records for unit tests. Never put real voter data here.
import type { AddressDto } from '../app/cesium/cesium.component';
import type { PaginationResult } from '../app/paginationResult';
import type { VoterDetailDto } from '../app/voter/voter.component';
import type { VoterDto } from '../app/voters/voters.component';

export function voterDetail(overrides: Partial<VoterDetailDto> = {}): VoterDetailDto {
  return {
    firstName: 'Alex',
    middleName: 'Quinn',
    lastName: 'Example',
    birthYear: 1990,
    registrationDate: '2020-03-15T00:00:00' as unknown as Date, // the API sends ISO strings
    addressId: 'addr-0001',
    fullStreetAddress: '123 Fake St',
    city: 'Testville',
    state: 'MI',
    zip: '49999',
    ...overrides,
  };
}

export function voterRow(overrides: Partial<VoterDto> = {}): VoterDto {
  return {
    id: 1001,
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
      { firstName: 'Alex', lastName: 'Example' },
      { firstName: 'Jordan', lastName: 'Sample' },
    ],
    ...overrides,
  };
}
