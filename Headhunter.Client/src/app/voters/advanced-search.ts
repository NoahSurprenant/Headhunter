// The voters page's advanced search: rows of field, operator and value, ANDed with the simple form. The API
// (SearchConditions.cs) takes the same field and operator names and checks them against its own whitelist.

export type FieldKind = 'text' | 'number' | 'date' | 'choice';

export interface Choice { value: string; label: string; }

export interface ConditionField {
  name: string;
  label: string;
  kind: FieldKind;
  /** For choice fields; the voter statuses come from the API instead (VoterStatuses). */
  choices?: Choice[];
}

export const CONDITION_FIELDS: ConditionField[] = [
  { name: 'firstName', label: 'First name', kind: 'text' },
  { name: 'middleName', label: 'Middle name', kind: 'text' },
  { name: 'lastName', label: 'Last name', kind: 'text' },
  { name: 'street', label: 'Street', kind: 'text' },
  { name: 'city', label: 'City', kind: 'text' },
  { name: 'zipCode', label: 'Zip code', kind: 'text' },
  { name: 'birthYear', label: 'Birth year', kind: 'number' },
  { name: 'age', label: 'Age', kind: 'number' },
  { name: 'registrationDate', label: 'Registered', kind: 'date' },
  { name: 'gender', label: 'Gender', kind: 'choice',
    choices: [{ value: 'F', label: 'Female' }, { value: 'M', label: 'Male' }, { value: 'X', label: 'X' }] },
  { name: 'permanentAbsentee', label: 'Permanent absentee', kind: 'choice',
    choices: [{ value: 'yes', label: 'Yes' }, { value: 'no', label: 'No' }] },
  { name: 'voterStatus', label: 'Voter status', kind: 'choice' },
];

export interface ConditionOperator { value: string; label: string; }

const RANGE = (eq: string, ne: string, lt: string, le: string, gt: string, ge: string): ConditionOperator[] => [
  { value: 'eq', label: eq },
  { value: 'ne', label: ne },
  { value: 'lt', label: lt },
  { value: 'le', label: le },
  { value: 'gt', label: gt },
  { value: 'ge', label: ge },
  { value: 'between', label: 'between' },
];

export const OPERATORS: Record<FieldKind, ConditionOperator[]> = {
  text: [
    { value: 'equals', label: 'equals' },
    { value: 'notEquals', label: 'is not' },
    { value: 'startsWith', label: 'starts with' },
    { value: 'endsWith', label: 'ends with' },
    { value: 'contains', label: 'contains' },
  ],
  number: RANGE('equals', 'is not', 'less than', 'at most', 'more than', 'at least'),
  date: RANGE('on', 'not on', 'before', 'on or before', 'after', 'on or after'),
  choice: [
    { value: 'is', label: 'is' },
    { value: 'isNot', label: 'is not' },
  ],
};

/** What a new row of each kind starts with: an operator that can use an index. */
const DEFAULT_OPERATOR: Record<FieldKind, string> = { text: 'startsWith', number: 'eq', date: 'ge', choice: 'is' };

/** These can't seek an index, so on 8.5M voters they may run into the API's timeout. */
export const SLOW_OPERATORS = new Set(['notEquals', 'endsWith', 'contains', 'ne', 'isNot']);

/** The API's Search:MaxConditions. */
export const MAX_CONDITIONS = 10;

export interface ConditionRow {
  /** For @for's track only; not kept in the URL. */
  id: number;
  field: string;
  op: string;
  value: string;
  /** The upper end, for "between". */
  value2: string;
}

/** As the API's SearchConditionDto. */
export interface SearchConditionDto {
  field: string;
  operator: string;
  value: string;
  value2?: string;
}

let nextId = 1;

export function fieldOf(name: string): ConditionField | undefined {
  return CONDITION_FIELDS.find(f => f.name === name);
}

export function operatorsFor(field: string): ConditionOperator[] {
  return OPERATORS[fieldOf(field)?.kind ?? 'text'];
}

export function newRow(field = 'lastName', choices: Choice[] = []): ConditionRow {
  const f = fieldOf(field)!;
  return { id: nextId++, field, op: DEFAULT_OPERATOR[f.kind], value: f.kind === 'choice' ? (choices[0]?.value ?? '') : '', value2: '' };
}

/**
 * The row with another field. Of the same type, the operator stays (and a text's value: a name typed under the
 * wrong field); otherwise it starts over with that type's default operator.
 */
export function withField(row: ConditionRow, field: string, choices: Choice[] = []): ConditionRow {
  const before = fieldOf(row.field)?.kind;
  const after = fieldOf(field)?.kind;
  if (!after) return row;
  if (before === after && after === 'text') return { ...row, field };
  if (before === after && after !== 'choice') return { ...row, field, value: '', value2: '' };
  return { ...newRow(field, choices), id: row.id };
}

/** Filled in enough to send: a value, and a second one for "between". */
export function isComplete(row: ConditionRow): boolean {
  return row.value.trim() !== '' && (row.op !== 'between' || row.value2.trim() !== '');
}

export function toDto(rows: ConditionRow[]): SearchConditionDto[] {
  return rows.filter(isComplete).map(r => ({
    field: r.field,
    operator: r.op,
    value: r.value.trim(),
    ...(r.op === 'between' ? { value2: r.value2.trim() } : {}),
  }));
}

// In the URL: ?cond=birthYear.between.1990.1999~lastName.startsWith.SUR. "." between a row's parts and "~" between
// rows; a value's own "%", "." and "~" are percent-encoded first, so "ST. CLAIR" survives as ST%2E CLAIR.
const escape = (value: string) => value.replace(/[%.~]/g, c => '%' + c.charCodeAt(0).toString(16).toUpperCase());

function unescape(value: string): string {
  try {
    return decodeURIComponent(value);
  } catch {
    return value;
  }
}

export function encodeConditions(rows: ConditionRow[]): string {
  return rows
    .map(r => [r.field, r.op, escape(r.value), ...(r.op === 'between' ? [escape(r.value2)] : [])].join('.'))
    .join('~');
}

/** The rows in a ?cond= value. Unknown fields or operators (an old or hand-edited link) are left out. */
export function decodeConditions(encoded: string | null | undefined): ConditionRow[] {
  if (!encoded) return [];
  return encoded.split('~')
    .map(part => part.split('.'))
    .filter(([field, op]) => operatorsFor(field).some(o => o.value === op) && !!fieldOf(field))
    .slice(0, MAX_CONDITIONS)
    .map(([field, op, value = '', value2 = '']) => ({
      id: nextId++, field, op, value: unescape(value), value2: op === 'between' ? unescape(value2) : '',
    }));
}

/** The conditions as the CSV download's query: conditions[0].field=..&conditions[0].operator=.. (ASP.NET's binding). */
export function appendConditionParams(query: URLSearchParams, conditions: SearchConditionDto[]): void {
  conditions.forEach((c, i) => {
    query.set(`conditions[${i}].field`, c.field);
    query.set(`conditions[${i}].operator`, c.operator);
    query.set(`conditions[${i}].value`, c.value);
    if (c.value2 !== undefined)
      query.set(`conditions[${i}].value2`, c.value2);
  });
}

/** What the page says when the API gave up on a search (422): Search:CommandTimeout ran out. */
export const TOO_BROAD_MESSAGE = 'This search is too broad to finish; add a narrower condition (e.g. a city or birth year)';

/** A search the API turned down (400) or couldn't finish (422): its message is shown above the list. */
export class SearchProblem extends Error {}
