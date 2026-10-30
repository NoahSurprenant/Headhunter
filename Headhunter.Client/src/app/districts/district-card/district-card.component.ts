import { ChangeDetectionStrategy, Component, computed, input, resource } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { fullName, genderLabel } from '../../shared/people/people';
import { DistrictRef, DistrictStatsDto, districtQuery, districtTitle } from '../districts';

/** Who lives in a district: counts, an age chart, the gender split and the newest registrations. */
@Component({
  selector: 'app-district-card',
  imports: [
    DatePipe,
    DecimalPipe,
    RouterLink,
  ],
  templateUrl: './district-card.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
})
export class DistrictCardComponent {
  district = input.required<DistrictRef>();

  stats = resource({
    params: () => this.district(),
    loader: async ({ params, abortSignal }) => {
      const response = await fetch(`api/district?${new URLSearchParams(districtQuery(params))}`, { signal: abortSignal });
      if (response.status === 404) return null;
      if (!response.ok) throw new Error(`District ${params.name}: ${response.status}`);
      return response.json() as Promise<DistrictStatsDto>;
    },
  });

  title = computed(() => districtTitle(this.district()));

  // The age bars' heights, as a share of the tallest.
  ages = computed(() => {
    const ages = this.stats.hasValue() ? this.stats.value()?.ages ?? [] : [];
    const max = Math.max(1, ...ages.map(a => a.count));
    return ages.map(a => ({ ...a, height: Math.round(100 * a.count / max) }));
  });

  genders = computed(() => {
    const s = this.stats.hasValue() ? this.stats.value() : null;
    if (!s) return [];
    return s.genders.map(g => ({
      label: genderLabel(g.gender) ?? 'Not given',
      count: g.count,
      percent: Math.round(1000 * g.count / s.voters) / 10,
    }));
  });

  /** The globe page showing this district. */
  globe = computed(() => {
    const d = this.district();
    return { districtType: d.type, district: d.name, districtWithin: d.within || null };
  });

  protected readonly fullName = fullName;
}
