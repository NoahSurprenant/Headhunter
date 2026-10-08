import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { DropdownComponent } from './dropdown.component';

@Component({
  imports: [DropdownComponent, ReactiveFormsModule],
  template: `
    <form [formGroup]="form">
      <x-dropdown formControlName="sign" [options]="options"></x-dropdown>
    </form>`,
})
class HostComponent {
  options = ['', 'Leo', 'Virgo'];
  form = new FormGroup({ sign: new FormControl('Virgo', { nonNullable: true }) });
}

describe('DropdownComponent', () => {
  async function render() {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    await fixture.whenStable(); // <option [value]> bindings settle after the select first writes its value
    const select = fixture.nativeElement.querySelector('select') as HTMLSelectElement;
    return { fixture, host: fixture.componentInstance, select };
  }

  it('renders one option per entry and reflects the initial form value', async () => {
    const { select } = await render();
    expect(Array.from(select.options).map(o => o.value)).toEqual(['', 'Leo', 'Virgo']);
    expect(select.value).toBe('Virgo');
  });

  it('writes the selected option back to the parent form control', async () => {
    const { select, host } = await render();
    select.value = 'Leo';
    select.dispatchEvent(new Event('change'));
    expect(host.form.controls.sign.value).toBe('Leo');
  });

  it('follows programmatic changes to the form control', async () => {
    const { fixture, select, host } = await render();
    host.form.controls.sign.setValue('Leo');
    fixture.detectChanges();
    expect(select.value).toBe('Leo');
  });
});
