import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { DatePipe } from '@angular/common';
import { approximateAge, fullName, registrationDateOrNull } from '../people/people';

/** Minimal shape the timeline needs; any voter DTO with these fields works. */
export interface TimelineVoter {
  id: string;
  firstName: string;
  middleName?: string | null;
  lastName: string;
  birthYear: number;
  registrationDate: string;
}

export interface TimelinePoint {
  id: string;
  name: string;
  age: number;
  registrationDate: string;
  /** Left offset along the track, 0-100, earliest to latest. */
  position: number;
}

export interface TimelineLayout {
  points: TimelinePoint[];
  /** Voters whose registration date is missing, unparseable, or a known placeholder (e.g. 1900-01-01). */
  unknown: TimelineVoter[];
}

/**
 * Places each voter with a usable registration date on a 0-100 horizontal track, earliest to latest. Voters
 * with a missing/unparseable/placeholder date are kept out of the track and returned separately so the caller
 * can still show them instead of silently dropping them.
 */
export function layoutTimeline(voters: TimelineVoter[], now = new Date()): TimelineLayout {
  const withDates = voters.map(voter => ({ voter, date: registrationDateOrNull(voter.registrationDate, now) }));
  const dated = withDates
    .filter((x): x is { voter: TimelineVoter; date: Date } => x.date !== null)
    .sort((a, b) => a.date.getTime() - b.date.getTime());
  const unknown = withDates.filter(x => x.date === null).map(x => x.voter);

  const min = dated[0]?.date.getTime() ?? 0;
  const max = dated[dated.length - 1]?.date.getTime() ?? 0;
  const range = max - min;

  const points = dated.map(({ voter, date }, i) => ({
    id: voter.id,
    name: fullName(voter),
    age: approximateAge(voter.birthYear, now),
    registrationDate: voter.registrationDate,
    // Evenly spread out when there's no range to work with (one dated voter, or several sharing a date).
    position: range > 0 ? ((date.getTime() - min) / range) * 100 : dated.length > 1 ? (i / (dated.length - 1)) * 100 : 50,
  }));

  return { points, unknown };
}

/** A horizontal "who moved in when" timeline for the voters at one address, earliest registration to latest. */
@Component({
  selector: 'app-registration-timeline',
  imports: [DatePipe],
  templateUrl: './registration-timeline.component.html',
  styleUrl: './registration-timeline.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RegistrationTimelineComponent {
  voters = input.required<TimelineVoter[]>();

  layout = computed(() => layoutTimeline(this.voters()));
  points = computed(() => this.layout().points);
  unknown = computed(() => this.layout().unknown);

  protected readonly fullName = fullName;
}
