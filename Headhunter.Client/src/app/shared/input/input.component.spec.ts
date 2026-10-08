import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { Observable, of } from 'rxjs';
import { InputComponent } from './input.component';

@Component({
  imports: [InputComponent, ReactiveFormsModule],
  template: `
    <form [formGroup]="form">
      <x-input formControlName="city" placeholder="Detroit" [onSearch]="search"></x-input>
      <x-input formControlName="age" type="number" [convertEmptyToNull]="true"></x-input>
    </form>`,
})
class HostComponent {
  form = new FormGroup({
    city: new FormControl('', { nonNullable: true }),
    age: new FormControl<number | string | null>(null),
  });
  queries: string[] = [];
  search = (query: string): Observable<string[]> => {
    this.queries.push(query);
    return of([`${query}ville`, `${query} Springs`]);
  };
}

describe('InputComponent', () => {
  afterEach(() => vi.useRealTimers());

  function render() {
    vi.useFakeTimers();
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    const [city, age] = Array.from(fixture.nativeElement.querySelectorAll('input')) as HTMLInputElement[];
    return { fixture, host: fixture.componentInstance, city, age };
  }

  function type(input: HTMLInputElement, value: string) {
    input.value = value;
    input.dispatchEvent(new Event('input'));
  }

  it('binds the native input to the parent form control', () => {
    const { host, city } = render();
    expect(city.placeholder).toBe('Detroit');
    type(city, 'Testville');
    expect(host.form.controls.city.value).toBe('Testville');
  });

  it('debounces suggestion lookups and ignores repeated values', async () => {
    const { host, city } = render();
    type(city, 'T');
    type(city, 'Te');
    type(city, 'Tes');
    await vi.advanceTimersByTimeAsync(299);
    expect(host.queries).toEqual([]);
    await vi.advanceTimersByTimeAsync(1);
    expect(host.queries).toEqual(['Tes']);

    type(city, 'Tes');
    await vi.advanceTimersByTimeAsync(300);
    expect(host.queries).toEqual(['Tes']);
  });

  it('shows suggestions while focused and fills the control when one is picked', async () => {
    const { fixture, host, city } = render();
    type(city, 'Fake');
    await vi.advanceTimersByTimeAsync(300);
    city.dispatchEvent(new Event('focus'));
    fixture.detectChanges();

    const items = Array.from(fixture.nativeElement.querySelectorAll('li')) as HTMLLIElement[];
    expect(items.map(li => li.textContent!.trim())).toEqual(['Fakeville', 'Fake Springs']);

    items[1].dispatchEvent(new Event('mousedown'));
    expect(host.form.controls.city.value).toBe('Fake Springs');

    city.dispatchEvent(new Event('blur'));
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('ul')).toBeNull();
  });

  it('does not look up suggestions for an empty value', async () => {
    const { host, city } = render();
    type(city, 'X');
    await vi.advanceTimersByTimeAsync(300);
    type(city, '');
    await vi.advanceTimersByTimeAsync(300);
    expect(host.queries).toEqual(['X']);
  });

  it('converts an emptied value to null when convertEmptyToNull is set', () => {
    const { host, age } = render();
    type(age, '21');
    // [type] is a dynamic binding, so Angular uses the default (string) value accessor;
    // VotersComponent.onSubmit converts birthYear/age with Number().
    expect(host.form.controls.age.value).toBe('21');
    type(age, '');
    expect(host.form.controls.age.value).toBeNull();
  });
});
